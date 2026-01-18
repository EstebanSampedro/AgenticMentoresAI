using Academikus.AnalysisMentoresVerdes.Entity.Analysis;

namespace Academikus.AnalysisMentoresVerdes.Business.Abstractions;

/// <summary>
/// Orquesta el análisis semanal de chats por ventana (domingo 00:00 a domingo 00:00).
/// </summary>
public interface IWeeklyAnalysisService
{
    /// <summary>
    /// Ejecuta el análisis para la ventana indicada (si es null, usa la semana anterior completa).
    /// Devuelve el RunId creado en AnalysisRun.
    /// </summary>
    Task<long> RunAsync(WeeklyWindow? window = null, bool dryRun = false, CancellationToken ct = default);
}
