using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public sealed class SummaryApiResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public SummaryApiData? Data { get; set; }
}

public sealed class SummaryApiData
{
    [JsonPropertyName("summary")] public SummaryPayload? Summary { get; set; }
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("session_id")] public string? SessionId { get; set; }
}

public sealed class SummaryPayload
{
    [JsonPropertyName("overview")] public string? Overview { get; set; }

    [JsonPropertyName("key_points")] public List<string>? KeyPoints { get; set; }

    [JsonPropertyName("escalated")] public bool Escalated { get; set; }

    [JsonPropertyName("escalation_reason")] public string? EscalationReason { get; set; }

    [JsonPropertyName("theme")] public string? Theme { get; set; }
}

