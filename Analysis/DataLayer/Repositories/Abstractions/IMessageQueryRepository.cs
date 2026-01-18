using Academikus.AnalysisMentoresVerdes.Entity.Analysis;

namespace Academikus.AnalysisMentoresVerdes.Data.Repositories.Abstractions;

public interface IMessageQueryRepository
{
    Task<IReadOnlyList<ChatTranscript>> GetTranscriptsAsync(WeeklyWindow window, CancellationToken ct);
}
