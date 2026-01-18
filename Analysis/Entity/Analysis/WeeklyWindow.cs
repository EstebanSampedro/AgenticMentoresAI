namespace Academikus.AnalysisMentoresVerdes.Entity.Analysis;

/// <summary>Ventana [inicio, fin) en UTC para una semana analizada.</summary>
public sealed record WeeklyWindow(DateTime WeekStartUtc, DateTime WeekEndUtc);
