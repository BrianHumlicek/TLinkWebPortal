using System.Text.Json.Serialization;

namespace TLinkWebPortal.Api.WebSocket.Models
{
    // Base message for polymorphic deserialization
    public abstract record WebSocketMessage
    {
        [JsonPropertyName("type")]
        public abstract string Type { get; }
    }

    #region Client → Server

    public record GetFullStateMessage : WebSocketMessage
    {
        public override string Type => "get_full_state";
    }

    public abstract record ArmCommandMessage : WebSocketMessage
    {
        [JsonPropertyName("session_id")]
        public required string SessionId { get; init; }

        [JsonPropertyName("partition_number")]
        public required byte PartitionNumber { get; init; }

        [JsonPropertyName("code")]
        public string? Code { get; init; }
    }

    public record ArmAwayMessage : ArmCommandMessage
    {
        public override string Type => "arm_away";
    }

    public record ArmHomeMessage : ArmCommandMessage
    {
        public override string Type => "arm_home";
    }

    public record ArmNightMessage : ArmCommandMessage
    {
        public override string Type => "arm_night";
    }

    public record DisarmMessage : ArmCommandMessage
    {
        public override string Type => "disarm";
    }

    #endregion

    #region Server → Client

    public record FullStateMessage : WebSocketMessage
    {
        public override string Type => "full_state";

        [JsonPropertyName("sessions")]
        public required List<SessionDto> Sessions { get; init; }
    }

    public record PartitionUpdateMessage : WebSocketMessage
    {
        public override string Type => "partition_update";

        [JsonPropertyName("session_id")]
        public required string SessionId { get; init; }

        [JsonPropertyName("partition_number")]
        public required byte PartitionNumber { get; init; }

        [JsonPropertyName("status")]
        public required string Status { get; init; }
    }

    public record ZoneUpdateMessage : WebSocketMessage
    {
        public override string Type => "zone_update";

        [JsonPropertyName("session_id")]
        public required string SessionId { get; init; }

        [JsonPropertyName("partition_number")]
        public required byte PartitionNumber { get; init; }

        [JsonPropertyName("zone_number")]
        public required byte ZoneNumber { get; init; }

        [JsonPropertyName("open")]
        public required bool Open { get; init; }
    }

    public record ErrorMessage : WebSocketMessage
    {
        public override string Type => "error";

        [JsonPropertyName("message")]
        public required string Message { get; init; }
    }

    #endregion

    #region DTOs

    public record SessionDto
    {
        [JsonPropertyName("session_id")]
        public required string SessionId { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("partitions")]
        public required List<PartitionDto> Partitions { get; init; }
    }

    public record PartitionDto
    {
        [JsonPropertyName("partition_number")]
        public required byte PartitionNumber { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("status")]
        public required string Status { get; init; }

        [JsonPropertyName("zones")]
        public required List<ZoneDto> Zones { get; init; }
    }

    public record ZoneDto
    {
        [JsonPropertyName("zone_number")]
        public required byte ZoneNumber { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("device_class")]
        public required string DeviceClass { get; init; }

        [JsonPropertyName("open")]
        public required bool Open { get; init; }
    }

    #endregion
}