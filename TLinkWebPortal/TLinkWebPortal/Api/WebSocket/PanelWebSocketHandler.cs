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

            _logger.LogInformation("PanelWebSocketHandler initialized. Subscribed to state change events.");

            _sessionMonitor.SessionsChanged += OnSessionsChanged;
            _partitionService.PartitionStateChanged += OnPartitionChanged;
            _partitionService.ZoneStateChanged += OnZoneChanged;
        }

        public async Task HandleConnectionAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                _logger.LogWarning("Rejected non-WebSocket request to /api/ws from {RemoteIp}", 
                    context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var clientId = Guid.NewGuid().ToString("N")[..8];
            _connectedClients.Add(webSocket);
            
            _logger.LogInformation("WebSocket client {ClientId} connected from {RemoteIp}. Total clients: {Count}",
                clientId, context.Connection.RemoteIpAddress, _connectedClients.Count);

            try
            {
                await ReceiveMessagesAsync(webSocket, clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket client {ClientId} error", clientId);
            }
            finally
            {
                _connectedClients.TryTake(out _);
                _logger.LogInformation("WebSocket client {ClientId} disconnected. Total clients: {Count}",
                    clientId, _connectedClients.Count);
                
                if (webSocket.State == WebSocketState.Open)
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }

        private async Task ReceiveMessagesAsync(System.Net.WebSockets.WebSocket webSocket, string clientId)
        {
            var buffer = new byte[4096];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogDebug("Client {ClientId} sent close frame", clientId);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogTrace("Client {ClientId} ← {Json}", clientId, json);
                    await ProcessMessageAsync(webSocket, json, clientId);
                }
            }
        }

        private async Task ProcessMessageAsync(System.Net.WebSockets.WebSocket webSocket, string json, string clientId)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                _logger.LogDebug("Client {ClientId} request: {Type}", clientId, type);

                switch (type)
                {
                    case "get_full_state":
                        await SendFullStateAsync(webSocket, clientId);
                        break;

                    case "arm_away":
                    case "arm_home":
                    case "arm_night":
                    case "disarm":
                        await HandleArmCommandAsync(webSocket, json, type, clientId);
                        break;

                    default:
                        _logger.LogWarning("Client {ClientId} sent unknown message type: {Type}", clientId, type);
                        await SendErrorAsync(webSocket, $"Unknown message type: {type}", clientId);
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Client {ClientId} sent invalid JSON: {Json}", clientId, json);
                await SendErrorAsync(webSocket, $"Invalid JSON: {ex.Message}", clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from client {ClientId}: {Json}", clientId, json);
                await SendErrorAsync(webSocket, $"Processing error: {ex.Message}", clientId);
            }
        }

        private async Task SendFullStateAsync(System.Net.WebSockets.WebSocket webSocket, string clientId)
        {
            var sessions = _sessionManager.GetActiveSessions()
                .Where(sessionId => !string.IsNullOrWhiteSpace(sessionId))
                .Select(sessionId => new SessionDto
                {
                    SessionId = sessionId,
                    Name = sessionId,
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

            _logger.LogDebug(
                "Client {ClientId} → full_state: {SessionCount} sessions, {PartitionCount} partitions, {ZoneCount} zones",
                clientId,
                sessions.Count,
                sessions.Sum(s => s.Partitions.Count),
                sessions.Sum(s => s.Partitions.Sum(p => p.Zones.Count)));

            var message = new FullStateMessage { Sessions = sessions };
            await SendMessageAsync(webSocket, message, clientId);
        }

        private async Task HandleArmCommandAsync(System.Net.WebSockets.WebSocket webSocket, string json, string type, string clientId)
        {
            _logger.LogDebug("Client {ClientId} sent {Command} (not implemented)", clientId, type);
            await SendErrorAsync(webSocket, $"Command '{type}' not yet implemented", clientId);
        }

        private async Task SendMessageAsync(System.Net.WebSockets.WebSocket webSocket, WebSocketMessage message, string clientId)
        {
            if (webSocket.State != WebSocketState.Open)
            {
                _logger.LogTrace("Skipping send to client {ClientId} (state: {State})", clientId, webSocket.State);
                return;
            }

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogTrace("Client {ClientId} → {MessageType}: {Json}", clientId, message.Type, json);

            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendErrorAsync(System.Net.WebSockets.WebSocket webSocket, string errorMessage, string clientId)
        {
            _logger.LogWarning("Client {ClientId} → error: {Message}", clientId, errorMessage);
            await SendMessageAsync(webSocket, new ErrorMessage { Message = errorMessage }, clientId);
        }

        #region Event Handlers (broadcast to all clients)

        private void OnSessionsChanged()
        {
            _logger.LogDebug("Session list changed, broadcasting full_state to {Count} clients", 
                _connectedClients.Count(c => c.State == WebSocketState.Open));
            _ = BroadcastFullStateAsync();
        }

        private void OnPartitionChanged(object? sender, PartitionStateChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.SessionId))
            {
                _logger.LogWarning("Ignoring partition update with empty sessionId");
                return;
            }

            _logger.LogDebug("Broadcasting partition_update: Session={SessionId}, Partition={Partition}, Status={Status}",
                e.SessionId, e.Partition.PartitionNumber, MapPartitionStatus(e.Partition));

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
            if (string.IsNullOrWhiteSpace(e.SessionId))
            {
                _logger.LogWarning("Ignoring zone update with empty sessionId");
                return;
            }

            _logger.LogDebug("Broadcasting zone_update: Session={SessionId}, Partition={Partition}, Zone={Zone}, Open={Open}",
                e.SessionId, e.PartitionNumber, e.Zone.ZoneNumber, e.Zone.IsOpen);

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
            var openClients = _connectedClients.Where(c => c.State == WebSocketState.Open).ToList();
            _logger.LogTrace("Broadcasting full_state to {Count} clients", openClients.Count);

            foreach (var client in openClients)
            {
                try
                {
                    await SendFullStateAsync(client, "broadcast");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting full state to client");
                }
            }
        }

        private async Task BroadcastMessageAsync(WebSocketMessage message)
        {
            var openClients = _connectedClients.Where(c => c.State == WebSocketState.Open).ToList();
            _logger.LogTrace("Broadcasting {MessageType} to {Count} clients", message.Type, openClients.Count);

            foreach (var client in openClients)
            {
                try
                {
                    await SendMessageAsync(client, message, "broadcast");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting {MessageType} to client", message.Type);
                }
            }
        }

        #endregion

        #region Helpers

        private static string MapPartitionStatus(Services.Models.PartitionState partition)
        {
            // TODO: Track actual armed state
            if (partition.IsArmed)
                return "armed_away";
            
            return partition.IsReady ? "disarmed" : "disarmed";
        }

        private static string DetermineDeviceClass(Services.Models.ZoneState zone)
        {
            // TODO: Zone type configuration
            return "door";
        }

        #endregion
    }
}