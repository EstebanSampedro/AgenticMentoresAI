using Academikus.AnalysisMentoresVerdes.Business.Abstractions;
using Academikus.AnalysisMentoresVerdes.Data.Repositories.Abstractions;
using Academikus.AnalysisMentoresVerdes.Entity.Analysis;
using Academikus.AnalysisMentoresVerdes.Entity.Options;
using Academikus.AnalysisMentoresVerdes.Proxy.AzureOpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Academikus.AnalysisMentoresVerdes.Business.Services;

public sealed class WeeklyAnalysisService : IWeeklyAnalysisService
{
    private readonly IMessageQueryRepository _queryRepo;
    private readonly IAnalysisWriteRepository _writeRepo;
    private readonly IGenerativeClient _ai;
    private readonly IOptions<AnalysisOptions> _options;
    private readonly ILogger<WeeklyAnalysisService> _log;

    public WeeklyAnalysisService(
     IMessageQueryRepository queryRepo,
     IAnalysisWriteRepository writeRepo,
     IGenerativeClient ai,
     IOptions<AnalysisOptions> options,
     ILogger<WeeklyAnalysisService> log)
    {
        _queryRepo = queryRepo ?? throw new ArgumentNullException(nameof(queryRepo));
        _writeRepo = writeRepo ?? throw new ArgumentNullException(nameof(writeRepo));
        _ai = ai ?? throw new ArgumentNullException(nameof(ai));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }


    public async Task<long> RunAsync(WeeklyWindow? window = null, bool dryRun = false, CancellationToken ct = default)
    {
        var tzId = _options.Value.Timezone ?? "America/Bogota";
        var tz = SafeFindTimeZone(tzId);

        // Si no viene ventana: usamos la semana anterior completa (domingo 00:00 a domingo 00:00).
        var w = window ?? PreviousWeekWindow(tz);

        _log.LogInformation("Weekly analysis starting. Window {StartUtc} -> {EndUtc} (tz: {Tz})",
            w.WeekStartUtc, w.WeekEndUtc, tz.StandardName);

        var runId = await _writeRepo.StartRunAsync(
            w,
            modelVersion: _options.Value.ModelVersion ?? "azure-openai",
            promptVersion: _options.Value.PromptVersion ?? "v1",
            ct);

        try
        {
            var transcripts = await _queryRepo.GetTranscriptsAsync(w, ct)
                  ?? new List<ChatTranscript>();
            _log.LogInformation("Found {Count} chat transcripts to analyze.", transcripts.Count);

            if (transcripts.Count == 0)
            {
                _log.LogWarning("No transcripts found for window {Start} -> {End}.", w.WeekStartUtc, w.WeekEndUtc);
                await _writeRepo.CompleteRunAsync(runId, success: true, ct);
                return runId;
            }

            var maxParallel = _options.Value.MaxDegreeOfParallelism > 0
                ? _options.Value.MaxDegreeOfParallelism
                : 3;

            var errors = new ConcurrentBag<(long ChatId, Exception Ex)>();

            using var throttle = new SemaphoreSlim(maxParallel);
            var tasks = transcripts.Select(async t =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    var result = await _ai.AnalyzeAsync(t, ct);
                    var raw = System.Text.Json.JsonSerializer.Serialize(result);

                    if (!dryRun)
                    {
                        await _writeRepo.SaveResultAsync(
                            runId, t.ChatId, w, result, raw, t.MentorId, t.StudentId, ct);
                    }

                    _log.LogDebug("Chat {ChatId} analyzed OK.", t.ChatId);
                }
                catch (Exception ex)
                {
                    errors.Add((t.ChatId, ex));
                    _log.LogError(ex, "Error analyzing ChatId={ChatId}", t.ChatId);
                }
                finally
                {
                    throttle.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            if (!errors.IsEmpty)
            {
                _log.LogWarning("Weekly analysis finished with {ErrorCount} chat(s) failing.", errors.Count);
            }

            await _writeRepo.CompleteRunAsync(runId, success: errors.IsEmpty, ct);
            _log.LogInformation("Weekly analysis completed. RunId={RunId}", runId);
        }
        catch
        {
            await _writeRepo.CompleteRunAsync(runId, success: false, ct);
            throw;
        }

        return runId;
    }

    private static TimeZoneInfo SafeFindTimeZone(string tzId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); // Bogotá (Windows)
        }
    }

    /// <summary>Ventana de la semana anterior completa en UTC (domingo->domingo).</summary>
    private static WeeklyWindow PreviousWeekWindow(TimeZoneInfo tz, DateTime? nowUtc = null)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc ?? DateTime.UtcNow, tz);
        var startThisWeekLocal = nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek); 
        var startPrevLocal = startThisWeekLocal.AddDays(-7);
        var endPrevLocal = startThisWeekLocal; 

        return new WeeklyWindow(
            TimeZoneInfo.ConvertTimeToUtc(startPrevLocal, tz),
            TimeZoneInfo.ConvertTimeToUtc(endPrevLocal, tz));
    }
}
