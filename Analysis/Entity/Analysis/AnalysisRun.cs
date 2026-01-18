using System.ComponentModel.DataAnnotations;

namespace Academikus.AnalysisMentoresVerdes.Entity.Analysis;


public sealed class AnalysisRun
{
    public long RunId { get; set; }              
    public DateTime WeekStartUtc { get; set; }
    public DateTime WeekEndUtc { get; set; }
    public string Status { get; set; } = "";
    public string? ModelVersion { get; set; }
    public string? PromptVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}