using System.ComponentModel.DataAnnotations;

namespace Academikus.AnalysisMentoresVerdes.Entity.Analysis;

public sealed class AnalysisParameterDefinition
{
    public int ParameterId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal? MinScore { get; set; }   
    public decimal? MaxScore { get; set; }  
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
