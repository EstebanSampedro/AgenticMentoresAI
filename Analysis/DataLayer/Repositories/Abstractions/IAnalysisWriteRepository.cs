using Academikus.AnalysisMentoresVerdes.Entity.Analysis;

namespace Academikus.AnalysisMentoresVerdes.Data.Repositories.Abstractions;

public interface IAnalysisWriteRepository
{
    Task<long> StartRunAsync(WeeklyWindow window, string modelVersion, string promptVersion, CancellationToken ct);
    Task SaveResultAsync(long runId, long chatId, WeeklyWindow window, AiAnalysisResult result, string rawJson, long? mentorId, long? studentId, CancellationToken ct);
    Task CompleteRunAsync(long runId, bool success, CancellationToken ct);
}
