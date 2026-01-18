// Data/Repositories/AnalysisWriteRepository.cs
using System.Data;
using Microsoft.Data.SqlClient;
using Academikus.AnalysisMentoresVerdes.Data.Repositories.Abstractions;
using Academikus.AnalysisMentoresVerdes.Entity.Analysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Academikus.AnalysisMentoresVerdes.Data.Repositories;

public sealed class AnalysisWriteRepository : IAnalysisWriteRepository
{
    private readonly string _connStr;
    private readonly ILogger<AnalysisWriteRepository> _log;

    // cache en memoria de ParameterId por Code
    private static Dictionary<string, int>? _paramCache;
    private static readonly SemaphoreSlim _paramLock = new(1, 1);

    public AnalysisWriteRepository(IConfiguration cfg, ILogger<AnalysisWriteRepository> log)
    {
        _connStr = cfg.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default not configured.");
        _log = log;
    }

    public async Task<long> StartRunAsync(WeeklyWindow window, string modelVersion, string promptVersion, CancellationToken ct)
    {
        using var con = new SqlConnection(_connStr);
        await con.OpenAsync(ct);

        const string sql = @"
INSERT INTO dbo.AnalysisRun(WeekStartUtc, WeekEndUtc, Status, ModelVersion, PromptVersion, CreatedAt)
OUTPUT INSERTED.RunId
VALUES(@s, @e, 'Running', @m, @p, SYSUTCDATETIME());";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@s", window.WeekStartUtc);
        cmd.Parameters.AddWithValue("@e", window.WeekEndUtc);
        cmd.Parameters.AddWithValue("@m", modelVersion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@p", promptVersion ?? (object)DBNull.Value);

        var runId = (long)await cmd.ExecuteScalarAsync(ct);
        _log.LogInformation("AnalysisRun started: {RunId}", runId);
        return runId;
    }

    public async Task SaveResultAsync(long runId, long chatId, WeeklyWindow window, AiAnalysisResult r, string rawJson, long? mentorId, long? studentId, CancellationToken ct)
    {
        using var con = new SqlConnection(_connStr);
        await con.OpenAsync(ct);

        var map = await GetParameterMapAsync(con, ct);

        using var tx = con.BeginTransaction();

        async Task Insert(string code, decimal score, string? content, string? subjectRole)
        {
            using var c = new SqlCommand(@"
INSERT INTO dbo.AnalysisObservation
(RunId, ChatId, ParameterId, WeekStartUtc, WeekEndUtc, ParameterScore, ParameterContent, MentorId, StudentId, SubjectRole, CreatedAt)
VALUES(@run,@chat,@pid,@ws,@we,@score,@content,@mentor,@student,@role, SYSUTCDATETIME());", con, tx);

            c.Parameters.AddWithValue("@run", runId);
            c.Parameters.AddWithValue("@chat", chatId);
            c.Parameters.AddWithValue("@pid", map[code]);
            c.Parameters.AddWithValue("@ws", window.WeekStartUtc);
            c.Parameters.AddWithValue("@we", window.WeekEndUtc);
            c.Parameters.AddWithValue("@score", score);
            c.Parameters.AddWithValue("@content", (object?)content ?? DBNull.Value);
            c.Parameters.AddWithValue("@mentor", (object?)mentorId ?? DBNull.Value);
            c.Parameters.AddWithValue("@student", (object?)studentId ?? DBNull.Value);
            c.Parameters.AddWithValue("@role", (object?)subjectRole ?? DBNull.Value);

            await c.ExecuteNonQueryAsync(ct);
        }

        await Insert("MISUNDERSTOOD_PCT", r.MisunderstoodPct, null, null);
        await Insert("EMPATHY_AI", r.EmpathyAi, null, "AI");
        await Insert("EMPATHY_MENTOR", r.EmpathyMentor, null, "Mentor");
        await Insert("SENTIMENT_STUDENT_START",0, r.SentimentStudentStart, "Student");
        await Insert("SENTIMENT_STUDENT_END", 0 ,r.SentimentStudentEnd, "Student");
        await Insert("EMOTION_AVG", r.EmotionAvg, null, "Student");
        await Insert("WARMTH_AI", r.WarmthAi, null, "AI");
        await Insert("WARMTH_MENTOR", r.WarmthMentor, null, "Mentor");

        using (var s = new SqlCommand(@"
INSERT INTO dbo.AnalysisSummary(RunId, ChatId, WeekStartUtc, WeekEndUtc, RawJson, CreatedAt)
VALUES(@run,@chat,@ws,@we,@raw, SYSUTCDATETIME());", con, tx))
        {
            s.Parameters.AddWithValue("@run", runId);
            s.Parameters.AddWithValue("@chat", chatId);
            s.Parameters.AddWithValue("@ws", window.WeekStartUtc);
            s.Parameters.AddWithValue("@we", window.WeekEndUtc);
            s.Parameters.AddWithValue("@raw", rawJson);
            await s.ExecuteNonQueryAsync(ct);
        }

        tx.Commit();
    }

    public async Task CompleteRunAsync(long runId, bool success, CancellationToken ct)
    {
        using var con = new SqlConnection(_connStr);
        await con.OpenAsync(ct);

        using var cmd = new SqlCommand("UPDATE dbo.AnalysisRun SET Status=@s WHERE RunId=@id;", con);
        cmd.Parameters.AddWithValue("@id", runId);
        cmd.Parameters.AddWithValue("@s", success ? "Completed" : "Failed");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------- helpers ----------
    private static async Task<Dictionary<string, int>> GetParameterMapAsync(SqlConnection con, CancellationToken ct)
    {
        if (_paramCache is not null) return _paramCache;

        await _paramLock.WaitAsync(ct);
        try
        {
            if (_paramCache is not null) return _paramCache;

            using var cmd = new SqlCommand("SELECT Code, ParameterId FROM dbo.AnalysisParameterDefinition WHERE IsActive=1;", con);
            using var rd = await cmd.ExecuteReaderAsync(ct);

            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            while (await rd.ReadAsync(ct))
            {
                var code = rd.GetString(0);
                var id = rd.GetInt32(1);
                dict[code] = id;
            }

            // Validación mínima:
            string[] required = new[]
            {
                "MISUNDERSTOOD_PCT","EMPATHY_AI","EMPATHY_MENTOR",
                "SENTIMENT_STUDENT_START","SENTIMENT_STUDENT_END",
                "EMOTION_AVG","WARMTH_AI","WARMTH_MENTOR"
            };
            var missing = required.Where(r => !dict.ContainsKey(r)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Missing AnalysisParameterDefinition codes: " + string.Join(",", missing));

            _paramCache = dict;
            return dict;
        }
        finally
        {
            _paramLock.Release();
        }
    }
}
