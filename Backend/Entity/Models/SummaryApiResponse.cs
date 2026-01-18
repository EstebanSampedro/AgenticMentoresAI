using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public sealed class SummaryApiResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("data")]
    public SummaryApiData? Data { get; set; }

    [JsonProperty("errors")]
    public string? Errors { get; set; }

    [JsonProperty("meta")]
    public string? Meta { get; set; }
}

public sealed class SummaryApiData
{
    [JsonProperty("summary")]
    public SummaryPayload? Summary { get; set; }

    [JsonProperty("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonProperty("session_id")]
    public string? SessionId { get; set; }
}

public sealed class SummaryPayload
{
    // Overview of the summarized conversation
    [JsonProperty("overview")]
    public string? Overview { get; set; }

    // Bullet key points identified by the AI
    [JsonProperty("key_points")]
    public List<string>? KeyPoints { get; set; }

    // Whether the conversation needs escalation
    [JsonProperty("escalated")]
    public bool Escalated { get; set; }

    // Reason for escalation, if applicable
    [JsonProperty("escalation_reason")]
    public string? EscalationReason { get; set; }

    // Topic or theme detected by the AI
    [JsonProperty("theme")]
    public string? Theme { get; set; }

    // Priority level detected or assigned
    [JsonProperty("priority")]
    public string? Priority { get; set; }
}
