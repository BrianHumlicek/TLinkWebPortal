using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DSC.TLink.ITv2.MediatR;
using TLinkWebPortal.Api.WebSocket.Models;
using TLinkWebPortal.Services;

namespace TLinkWebPortal.Api.WebSocket
{
    /// <summary>
    /// Manages WebSocket connections for Home Assistant integration.
    /// Pushes partition and zone updates in real-time.
    /// </summary>
    public class PanelWebSocketHandler
    {
        private readonly IPartitionStatusService _partitionService;
        private readonly IITv2SessionManager _sessionManager;
        private readonly ISessionMonitor _sessionMonitor;
        private readonly ILogger<PanelWebSocketHandler> _logger;
        private readonly ConcurrentBag<System.Net.WebSockets.WebSocket> _connectedClients = new();

        public PanelWebSocketHandler(
            IPartitionStatusService partitionService,
            IITv2SessionManager sessionManager,
            ISessionMonitor sessionMonitor,
            ILogger<PanelWebSocketHandler> logger)
        {
            _partitionService = partitionService;
            _sessionManager = sessionManager;
            _sessionMonitor = sessionMonitor;
            _logger = logger;

            // Subscribe to state changes
            _sessionMonitor.SessionsChanged += OnSessionsChanged;
            _partitionService.PartitionStateChanged += OnPartitionChanged;
            _partitionService.ZoneStateChanged += OnZoneChanged;
        }

        public async Task HandleConnectionAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            _connectedClients.Add(webSocket);
            _logger.LogInformation("WebSocket client connected. Total clients: {Count}", _connectedClients.Count);

            try
            {
                await ReceiveMessagesAsync(webSocket);
            }
            finally
            {
                _connectedClients.TryTake(out _);
                _logger.LogInformation("WebSocket client disconnected. Total clients: {Count}", _connectedClients.Count);
                
                if (webSocket.State == WebSocketState.Open)
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }

        private async Task ReceiveMessagesAsync(System.Net.WebSockets.WebSocket webSocket)
        {
            var buffer = new byte[4096];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(webSocket, json);
                }
            }
        }

        private async Task ProcessMessageAsync(System.Net.WebSockets.WebSocket webSocket, string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "get_full_state":
                        await SendFullStateAsync(webSocket);
                        break;

                    case "arm_away":
                    case "arm_home":
                    case "arm_night":
                    case "disarm":
                        await HandleArmCommandAsync(webSocket, json, type);
                        break;

                    default:
                        await SendErrorAsync(webSocket, $"Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message: {Json}", json);
                await SendErrorAsync(webSocket, $"Invalid message format: {ex.Message}");
            }
        }

        private async Task SendFullStateAsync(System.Net.WebSockets.WebSocket webSocket)
        {
            var sessions = _sessionManager.GetActiveSessions()
                .Select(sessionId => new SessionDto
                {
                    SessionId = sessionId,
                    Name = sessionId, // TODO: Add friendly names
                    Partitions = _partitionService.GetPartitions(sessionId)
                        .Select(kvp => new PartitionDto
                        {
                            PartitionNumber = kvp.Key,
                            Name = $"Partition {kvp.Key}",
                            Status = MapPartitionStatus(kvp.Value),
                            Zones = kvp.Value.Zones
                                .Select(z => new ZoneDto
                                {
                                    ZoneNumber = z.Key,
                                    Name = string.IsNullOrEmpty(z.Value.ZoneName) ? $"Zone {z.Key}" : z.Value.ZoneName,
                                    DeviceClass = DetermineDeviceClass(z.Value),
                                    Open = z.Value.IsOpen
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            var message = new FullStateMessage { Sessions = sessions };
            await SendMessageAsync(webSocket, message);
        }

        private async Task HandleArmCommandAsync(System.Net.WebSockets.WebSocket webSocket, string json, string type)
        {
            // TODO: Implement panel command sending via MediatR
            // For now, just acknowledge
            await SendErrorAsync(webSocket, $"Command '{type}' not yet implemented");
        }

        private async Task SendMessageAsync(System.Net.WebSockets.WebSocket webSocket, WebSocketMessage message)
        {
            if (webSocket.State != WebSocketState.Open)
                return;

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendErrorAsync(System.Net.WebSockets.WebSocket webSocket, string message)
        {
            await SendMessageAsync(webSocket, new ErrorMessage { Message = message });
        }

        #region Event Handlers (broadcast to all clients)

        private void OnSessionsChanged()
        {
            // When sessions change, send full state to all clients
            _ = BroadcastFullStateAsync();
        }

        private void OnPartitionChanged(object? sender, PartitionStateChangedEventArgs e)
        {
            var message = new PartitionUpdateMessage
            {
                SessionId = e.SessionId,
                PartitionNumber = e.Partition.PartitionNumber,
                Status = MapPartitionStatus(e.Partition)
            };

            _ = BroadcastMessageAsync(message);
        }

        private void OnZoneChanged(object? sender, ZoneStateChangedEventArgs e)
        {
            var message = new ZoneUpdateMessage
            {
                SessionId = e.SessionId,
                PartitionNumber = e.PartitionNumber,
                ZoneNumber = e.Zone.ZoneNumber,
                Open = e.Zone.IsOpen
            };

            _ = BroadcastMessageAsync(message);
        }

        private async Task BroadcastFullStateAsync()
        {
            foreach (var client in _connectedClients.Where(c => c.State == WebSocketState.Open))
            {
                try
                {
                    await SendFullStateAsync(client);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting full state to client");
                }
            }
        }

        private async Task BroadcastMessageAsync(WebSocketMessage message)
        {
            foreach (var client in _connectedClients.Where(c => c.State == WebSocketState.Open))
            {
                try
                {
                    await SendMessageAsync(client, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting message to client");
                }
            }
        }

        #endregion

        #region Helpers

        private static string MapPartitionStatus(Services.Models.PartitionState partition)
        {
            // TODO: Track actual armed state - for now infer from IsReady
            if (partition.IsArmed)
                return "armed_away"; // Need to track arm mode
            
            return partition.IsReady ? "disarmed" : "disarmed";
        }

        private static string DetermineDeviceClass(Services.Models.ZoneState zone)
        {
            // TODO: Zone type configuration - for now default to door
            // Could be determined by zone number ranges or configured per-zone
            return "door";
        }

        #endregion
    }
}