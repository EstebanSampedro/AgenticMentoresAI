namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.DTOs;

public sealed class ChatSummaryDto
{
    public int Id { get; set; }
    public string ChatId { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public string SummaryType { get; set; } = default!;
    public string? KeyPoints { get; set; }
    public string? Escalated { get; set; }
    public string? EscalationReason { get; set; }
    public string? Theme { get; set; }
    public string? Priority { get; set; }
    public string CreatedAt { get; set; } = default!;
    public string CreatedBy { get; set; } = default!;
}
