using System.ComponentModel.DataAnnotations;

namespace Academikus.AnalysisMentoresVerdes.Entity.Analysis;

public sealed class AnalysisObservation
{
    public long ObservationId { get; set; }      // bigint
    public long RunId { get; set; }              // bigint (FK)
    public long ChatId { get; set; }             // bigint
    public int ParameterId { get; set; }        // int (FK)
    public DateTime WeekStartUtc { get; set; }
    public DateTime WeekEndUtc { get; set; }
    public decimal? ParameterScore { get; set; } // decimal(9,3) null
    public string? ParameterContent { get; set; }
    public long? MentorId { get; set; }           // bigint
    public long? StudentId { get; set; }          // bigint
    public string SubjectRole { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
