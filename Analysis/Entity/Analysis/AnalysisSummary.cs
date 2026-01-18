using System.ComponentModel.DataAnnotations;

namespace Academikus.AnalysisMentoresVerdes.Entity.Analysis;

public sealed class AnalysisSummary
{
    public long SummaryId { get; set; }          // bigint
    public long RunId { get; set; }              // bigint (FK)
    public long ChatId { get; set; }             // bigint
    public DateTime WeekStartUtc { get; set; }
    public DateTime WeekEndUtc { get; set; }
    public string RawJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
