// UI/Controllers/AnalysisController.cs
using Academikus.AnalysisMentoresVerdes.Business.Abstractions;
using Academikus.AnalysisMentoresVerdes.Entity.Analysis;
using Academikus.AnalysisMentoresVerdes.Entity.Options;
using Academikus.AnalysisMentoresVerdes.Proxy.AzureOpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Linq;
using System.Text.Json;
using E = Academikus.AnalysisMentoresVerdes.Entity.Analysis;


namespace Academikus.AnalysisMentoresVerdes.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AnalysisController : ControllerBase
{
    private readonly IWeeklyAnalysisService _service;
    private readonly IOptions<AnalysisOptions> _opts;
    private readonly ILogger<AnalysisController> _log;
    private readonly IGenerativeClient _ai;

    public AnalysisController(
        IWeeklyAnalysisService service,
        IOptions<AnalysisOptions> opts,
        ILogger<AnalysisController> log,
        IGenerativeClient ai)
        => (_service, _opts, _log, _ai) = (service, opts, log, ai);



    /// <summary>
    /// Ejecuta el análisis semanal. Si no envías weekStart, toma la semana anterior completa.
    /// Ejemplo: POST /api/analysis/run?weekStart=2025-08-24&dryRun=false
    /// </summary>
    //[HttpPost("run")]
    //public async Task<IActionResult> Run(DateTime? weekStart, bool dryRun = false, string? tz = null, CancellationToken ct = default)
    //{
    //    try
    //    {
    //        E.WeeklyWindow? window = null;

    //        if (weekStart.HasValue)
    //        {
    //            var tzId = tz ?? _opts.Value.Timezone ?? "America/Bogota";
    //            var zone = TryFindTimeZone(tzId);

    //            var local = DateTime.SpecifyKind(weekStart.Value.Date, DateTimeKind.Unspecified);
    //            var startOfWeekLocal = local.AddDays(-(int)local.DayOfWeek);
    //            var endOfWeekLocal = startOfWeekLocal.AddDays(7);

    //            window = new E.WeeklyWindow(
    //                TimeZoneInfo.ConvertTimeToUtc(startOfWeekLocal, zone),
    //                TimeZoneInfo.ConvertTimeToUtc(endOfWeekLocal, zone));
    //        }

    //        var runId = await _service.RunAsync(window, dryRun, HttpContext.RequestAborted);
    //        _log.LogInformation("Analysis run requested via API. RunId={RunId}", runId);
    //        return Ok(new { runId, window?.WeekStartUtc, window?.WeekEndUtc, dryRun });
    //    }
    //    catch (Exception ex)
    //    {
    //        _log.LogError(ex, "Run failed");
    //        return StatusCode(500, new { error = ex.ToString() });
    //    }
    //}
    //[HttpGet("db/health")]
    //public async Task<IActionResult> DbHealth([FromServices] AnalysisDbContext db, CancellationToken ct)
    //{
    //    var ok = await db.Database.CanConnectAsync(ct);
    //    var cs = db.Database.GetDbConnection().ConnectionString;
    //    return Ok(new { ok, server = db.Database.GetDbConnection().DataSource, database = db.Database.GetDbConnection().Database });
    //}
    //[HttpGet("db/sample-raw")]
    //public async Task<IActionResult> DbSampleRaw(
    //[FromQuery] DateTime weekStart,                 // requerido
    //[FromServices] AnalysisDbContext db,           // requerido
    //[FromQuery] string tz = "America/Bogota",      // opcional
    //[FromQuery] int take = 50,                     // opcional
    //CancellationToken ct = default)
    //{
    //    try
    //    {
    //        // Ventana [domingo..domingo) en UTC
    //        var zone = TryFindTimeZone(tz);
    //        var local = DateTime.SpecifyKind(weekStart.Date, DateTimeKind.Unspecified);
    //        var startLocal = local.AddDays(-(int)local.DayOfWeek);
    //        var endLocal = startLocal.AddDays(7);
    //        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, zone);
    //        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, zone);

    //        const string sql = @"
    //            SELECT TOP (@take)
    //                c.Id           AS ChatId,
    //                c.MentorId,
    //                c.StudentId,
    //                m.CreatedAt,
    //                m.SenderRole,
    //                m.MessageContent
    //            FROM dbo.Chat c
    //            JOIN dbo.Conversation cv ON cv.ChatId = c.Id
    //            JOIN dbo.Message m       ON m.ConversationId = cv.Id
    //            WHERE m.CreatedAt >= @startUtc AND m.CreatedAt < @endUtc
    //            ORDER BY c.Id, m.CreatedAt;";

    //        var rows = new List<object>();

    //        var cs = db.Database.GetDbConnection().ConnectionString;
    //        await using var conn = new SqlConnection(cs);
    //        await conn.OpenAsync(ct);
    //        await using var cmd = new SqlCommand(sql, conn);

    //        cmd.Parameters.Add(new SqlParameter("@take", SqlDbType.Int) { Value = take });
    //        cmd.Parameters.Add(new SqlParameter("@startUtc", SqlDbType.DateTime2) { Value = startUtc });
    //        cmd.Parameters.Add(new SqlParameter("@endUtc", SqlDbType.DateTime2) { Value = endUtc });

    //        await using var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

    //        // Ordinales seguros
    //        int ordChatId = rdr.GetOrdinal("ChatId");
    //        int ordMentorId = rdr.GetOrdinal("MentorId");
    //        int ordStudentId = rdr.GetOrdinal("StudentId");
    //        int ordCreatedAt = rdr.GetOrdinal("CreatedAt");
    //        int ordSenderRole = rdr.GetOrdinal("SenderRole");
    //        int ordMessage = rdr.GetOrdinal("MessageContent");

    //        while (await rdr.ReadAsync(ct))
    //        {
    //            int chatId = rdr.GetInt32(0); 
    //            int mentorId = rdr.GetInt32(1);
    //            int studentId = rdr.GetInt32(2);
    //            DateTime createdAt = rdr.GetDateTime(3);
    //            string senderRole = rdr.IsDBNull(4) ? "" : rdr.GetString(4);
    //            string message = rdr.IsDBNull(5) ? "" : rdr.GetString(5);

    //            rows.Add(new { chatId, mentorId, studentId, createdAt, senderRole, message });
    //        }

    //        return Ok(new
    //        {
    //            window = new { startUtc, endUtc },
    //            count = rows.Count,
    //            sample = rows
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        // Evita que el middleware lo “200 OK-tee”
    //        return StatusCode(500, new { error = ex.ToString() });
    //    }
    //}
    private async Task<List<E.ChatTranscript>> FetchTranscriptsAsync(
    AnalysisDbContext db,
    DateTime weekStartLocal,
    string tz,
    int take,
    CancellationToken ct)
    {
        var zone = TryFindTimeZone(tz);
        var startLocal = DateTime.SpecifyKind(weekStartLocal.Date, DateTimeKind.Unspecified)
                                  .AddDays(-(int)weekStartLocal.DayOfWeek);
        var endLocal = startLocal.AddDays(7);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, zone);

        const string sql = @"
            SELECT TOP (@take)
                c.Id           AS ChatId,
                c.MentorId,
                c.StudentId,
                m.CreatedAt,
                m.SenderRole,
                m.MessageContent
            FROM dbo.Chat c
            JOIN dbo.Conversation cv ON cv.ChatId = c.Id
            JOIN dbo.Message m       ON m.ConversationId = cv.Id
            WHERE m.CreatedAt >= @startUtc AND m.CreatedAt < @endUtc
            ORDER BY c.Id, m.CreatedAt;";

        var cs = db.Database.GetDbConnection().ConnectionString;
        // 👇 Cambiar a long en la tupla
        var rows = new List<(long chatId, long? mentorId, long? studentId, DateTime createdAt, string role, string text)>();

        await using (var conn = new SqlConnection(cs))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@take", SqlDbType.Int) { Value = take });
            cmd.Parameters.Add(new SqlParameter("@startUtc", SqlDbType.DateTime2) { Value = startUtc });
            cmd.Parameters.Add(new SqlParameter("@endUtc", SqlDbType.DateTime2) { Value = endUtc });

            await using var rdr = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            while (await rdr.ReadAsync(ct))
            {
                // 👇 Cast explícito de int a long
                var chatId = (long)rdr.GetInt32(0);
                var mentorId = rdr.IsDBNull(1) ? (long?)null : (long)rdr.GetInt32(1);
                var studentId = rdr.IsDBNull(2) ? (long?)null : (long)rdr.GetInt32(2);
                var createdAt = rdr.GetDateTime(3);
                var roleRaw = rdr.IsDBNull(4) ? "" : rdr.GetString(4);
                var text = rdr.IsDBNull(5) ? "" : rdr.GetString(5);
                rows.Add((chatId, mentorId, studentId, createdAt, roleRaw, text));
            }
        }

        var transcripts = rows
            .GroupBy(r => new { r.chatId, r.mentorId, r.studentId })
            .Select(g =>
            {
                var turns = g.Select(r =>
                {
                    var (role, ia) = MapRole(r.role);
                    return new E.ChatMessageTurn(
                        CreatedAtUtc: r.createdAt,
                        SenderRole: role,
                        Text: r.text,
                        IAEnabled: ia
                    );
                })
                .OrderBy(t => t.CreatedAtUtc)
                .ToList();

                // ✅ Ahora los tipos coinciden: long → long
                return new E.ChatTranscript(
                    ChatId: g.Key.chatId,
                    MentorId: g.Key.mentorId,
                    StudentId: g.Key.studentId,
                    Turns: turns
                );
            })
            .ToList();

        return transcripts;
    }

    private static (string role, bool ia) MapRole(string senderRoleRaw)
    {
        var s = (senderRoleRaw ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "ia" => ("IA", true),
            "estudiante" => ("Estudiante", false),
            "mentor" => ("Mentor", false),
            _ => (senderRoleRaw ?? "", false)
        };
    }

    /// <summary>
    /// Construye transcripciones desde DB y las envía al modelo de Azure OpenAI.
    /// No persiste; sirve para validar el pipeline completo DB → AOAI → JSON.
    /// </summary>
    //[HttpPost("analyze-now")]
    //public async Task<IActionResult> AnalyzeNow(
    //    [FromServices] AnalysisDbContext db,
    //    [FromQuery] DateTime weekStart,                // cualquier día de la semana
    //    [FromQuery] string tz = "America/Bogota",
    //    [FromQuery] int take = 200,                    // límite de mensajes totales en la ventana
    //    CancellationToken ct = default)
    //{
    //    try
    //    {
    //        var transcripts = await FetchTranscriptsAsync(db, weekStart, tz, take, ct);
    //        if (transcripts.Count == 0)
    //            return Ok(new { info = "No transcripts in that window." });

    //        var results = new List<object>();

    //        foreach (var t in transcripts)
    //        {
    //            var ai = await _ai.AnalyzeAsync(t, ct); // 👈 tu cliente ya formatea prompt y parsea JSON
    //            results.Add(new
    //            {
    //                t.ChatId,
    //                t.MentorId,
    //                t.StudentId,
    //                ai
    //            });
    //        }

    //        return Ok(new
    //        {
    //            analyzedChats = results.Count,
    //            results
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        _log.LogError(ex, "AnalyzeNow failed");
    //        return StatusCode(500, new { error = ex.ToString() });
    //    }
    //}
    //// UI/Controllers/AnalysisController.cs  (añade esto al final del controller)


    [HttpPost("analyze-save")]
    public async Task<IActionResult> AnalyzeAndSave(
    [FromServices] AnalysisDbContext db,
    [FromQuery] DateTime weekStart,
    [FromQuery] string tz = "America/Bogota",
    [FromQuery] int take = 200,
    [FromQuery] bool forceReanalyze = false, 
    CancellationToken ct = default)
    {
        var zone = TryFindTimeZone(tz);
        var local = DateTime.SpecifyKind(weekStart.Date, DateTimeKind.Unspecified);
        var startLocal = local.AddDays(-(int)local.DayOfWeek);
        var endLocal = startLocal.AddDays(7);
        var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, zone);
        var weekEndUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, zone);

        // 1️⃣ Obtener TODOS los transcripts de la ventana
        var transcripts = await FetchTranscriptsAsync(db, weekStart, tz, take, ct);
        if (transcripts.Count == 0)
            return Ok(new { info = "No transcripts in that window." });

        // 2️⃣ Filtrar los que YA fueron analizados (a menos que force=true)
        var analyzedChatIds = new HashSet<long>();
        if (!forceReanalyze)
        {
            analyzedChatIds = await db.AnalysisSummaries
                .Where(s => s.WeekStartUtc == weekStartUtc && s.WeekEndUtc == weekEndUtc)
                .Select(s => s.ChatId)
                .ToHashSetAsync(ct);

            _log.LogInformation(
                "Found {Count} chats already analyzed for week {Week}",
                analyzedChatIds.Count, weekStartUtc.ToString("yyyy-MM-dd"));
        }

        // 3️⃣ Procesar solo los chats NO analizados
        var toAnalyze = transcripts
            .Where(t => !analyzedChatIds.Contains(t.ChatId))
            .ToList();

        if (toAnalyze.Count == 0)
        {
            return Ok(new
            {
                info = "All chats already analyzed for this week.",
                weekStartUtc,
                weekEndUtc,
                totalChats = transcripts.Count,
                alreadyAnalyzed = analyzedChatIds.Count,
                newlyAnalyzed = 0
            });
        }

        _log.LogInformation(
            "Analyzing {New} new chats (skipping {Existing} already analyzed)",
            toAnalyze.Count, analyzedChatIds.Count);

        // 4️⃣ Analizar solo los nuevos
        var results = new List<(ChatTranscript chat, dynamic ai)>();
        foreach (var t in toAnalyze)
        {
            var ai = await _ai.AnalyzeAsync(t, ct);
            results.Add((t, ai));
        }

        await using var trx = await db.Database.BeginTransactionAsync(ct);

        // 5️⃣ Obtener o crear Run
        var run = await db.AnalysisRuns
            .SingleOrDefaultAsync(x => x.WeekStartUtc == weekStartUtc && x.WeekEndUtc == weekEndUtc, ct);

        if (run is null)
        {
            run = new AnalysisRun
            {
                WeekStartUtc = weekStartUtc,
                WeekEndUtc = weekEndUtc,
                Status = "Completed",
                ModelVersion = _opts.Value.ModelVersion ?? "gpt-4o-mini",
                PromptVersion = _opts.Value.PromptVersion ?? "v1",
                CreatedAt = DateTime.UtcNow
            };
            db.AnalysisRuns.Add(run);
            await db.SaveChangesAsync(ct);
        }


        //  Crear parámetros base (si no existen)
        var requiredParameters = new (string Code, string Name, decimal Min, decimal Max, string? Desc)[]
        {
        ("MisunderstoodPct", "Porcentaje de malentendidos", 0,100, "Porcentaje estimado de malentendidos"),
        ("EmpathyAI", "Empatía IA", 0,10, null),
        ("EmpathyMentor", "Empatía Mentor", 0,10, null),
        ("SentimentStudentStart", "Sentimiento inicio estudiante",0,0, "Texto libre con el resumen"),
        ("SentimentStudentEnd", "Sentimiento fin estudiante",0,0,"Texto libre con el resumen"),
        ("EmotionAvg", "Promedio emoción", 0,10, null),
        ("WarmthAI", "Calidez IA", 0,10, null),
        ("WarmthMentor", "Calidez Mentor", 0,10, null),
        ("OverallComment", "Comentario global", 0,0, "Texto libre con el resumen"),
        ("SatisfiedUser", "Usuario satisfecho", 0, 0, "Indica si la solicitud del usuario fue resuelta (true/false)"),
        ("Issue", "Tema principal", 0, 0, "Tema tratado: Pregunta frecuente o Justificación de inasistencia")
        };

        var existing = await db.AnalysisParameterDefinitions.AsNoTracking().ToListAsync(ct);
        var byCode = existing.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var p in requiredParameters)
        {
            if (!byCode.ContainsKey(p.Code))
            {
                var np = new AnalysisParameterDefinition
                {
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Desc,
                    MinScore = p.Min,
                    MaxScore = p.Max,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };
                db.AnalysisParameterDefinitions.Add(np);
                await db.SaveChangesAsync(ct);
                byCode[p.Code] = np;
            }
        }

        int P(string code) => byCode[code].ParameterId;

        // 7️⃣ Insertar Summaries y Observations solo para los NUEVOS
        foreach (var (chat, ai) in results)
        {
            db.AnalysisSummaries.Add(new AnalysisSummary
            {
                RunId = run.RunId,
                ChatId = chat.ChatId,
                WeekStartUtc = weekStartUtc,
                WeekEndUtc = weekEndUtc,
                RawJson = JsonSerializer.Serialize(ai),
                CreatedAt = DateTime.UtcNow
            });

            db.AnalysisObservations.AddRange(new[]
            {
            new AnalysisObservation {
                RunId = run.RunId, ChatId = chat.ChatId,
                ParameterId = P("MisunderstoodPct"),
                ParameterScore = Score(ai, "MisunderstoodPct"),
                WeekStartUtc = weekStartUtc, WeekEndUtc = weekEndUtc,
                MentorId = chat.MentorId, StudentId = chat.StudentId,
                SubjectRole = "Global", CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId, ChatId = chat.ChatId,
                ParameterId = P("EmpathyAI"),
                ParameterScore = Score(ai, "EmpathyAI", "EmpathyIa", "Empathy"),
                WeekStartUtc = weekStartUtc, WeekEndUtc = weekEndUtc,
                MentorId = chat.MentorId, StudentId = chat.StudentId,
                SubjectRole = "IA", CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId, ChatId = chat.ChatId,
                ParameterId = P("EmpathyMentor"),
                ParameterScore = Score(ai, "EmpathyMentor"),
                WeekStartUtc = weekStartUtc, WeekEndUtc = weekEndUtc,
                MentorId = chat.MentorId, StudentId = chat.StudentId,
                SubjectRole = "Mentor", CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId,
                ChatId = chat.ChatId,
                ParameterId = P("SentimentStudentStart"),
                WeekStartUtc = weekStartUtc,
                WeekEndUtc = weekEndUtc,
                ParameterScore = null, // <-- es texto, no número
                ParameterContent = Text(ai, "SentimentStudentStart", "sentiment_student_start"),
                MentorId = chat.MentorId,
                StudentId = chat.StudentId,
                SubjectRole = "Estudiante",
                CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId,
                ChatId = chat.ChatId,
                ParameterId = P("SentimentStudentEnd"),
                WeekStartUtc = weekStartUtc,
                WeekEndUtc = weekEndUtc,
                ParameterScore = null, // <-- es texto, no número
                ParameterContent = Text(ai, "SentimentStudentEnd", "sentiment_student_end"),
                MentorId = chat.MentorId,
                StudentId = chat.StudentId,
                SubjectRole = "Estudiante",
                CreatedAt = DateTime.UtcNow
            },

            new AnalysisObservation {
                RunId = run.RunId, ChatId = chat.ChatId,
                ParameterId = P("EmotionAvg"),
                ParameterScore = Score(ai, "EmotionAvg"),
                WeekStartUtc = weekStartUtc, WeekEndUtc = weekEndUtc,
                MentorId = chat.MentorId, StudentId = chat.StudentId,
                SubjectRole = "Global", CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId, ChatId = chat.ChatId,
                ParameterId = P("WarmthAI"),
                ParameterScore = Score(ai, "WarmthAI", "WarmthIa", "Warmth"),
                WeekStartUtc = weekStartUtc, WeekEndUtc = weekEndUtc,
                MentorId = chat.MentorId, StudentId = chat.StudentId,
                SubjectRole = "IA", CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId, ChatId = chat.ChatId,
                ParameterId = P("WarmthMentor"),
                ParameterScore = Score(ai, "WarmthMentor"),
                WeekStartUtc = weekStartUtc, WeekEndUtc = weekEndUtc,
                MentorId = chat.MentorId, StudentId = chat.StudentId,
                SubjectRole = "Mentor", CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId,
                ChatId = chat.ChatId,
                ParameterId = P("SatisfiedUser"),
                WeekStartUtc = weekStartUtc,
                WeekEndUtc = weekEndUtc,
                ParameterScore = null, // Es texto, no número
                ParameterContent = Text(ai, "SatisfiedUser"),
                MentorId = chat.MentorId,
                StudentId = chat.StudentId,
                SubjectRole = "Global",
                CreatedAt = DateTime.UtcNow
            },
            new AnalysisObservation {
                RunId = run.RunId,
                ChatId = chat.ChatId,
                ParameterId = P("Issue"),
                WeekStartUtc = weekStartUtc,
                WeekEndUtc = weekEndUtc,
                ParameterScore = null, // Es texto, no número
                ParameterContent = Text(ai, "Issue"),
                MentorId = chat.MentorId,
                StudentId = chat.StudentId,
                SubjectRole = "Global",
                CreatedAt = DateTime.UtcNow
            }
        });

            var comment = Text(ai, "OverallComment", "Overall", "Comment", "Resumen");
            if (!string.IsNullOrWhiteSpace(comment))
            {
                db.AnalysisObservations.Add(new AnalysisObservation
                {
                    RunId = run.RunId,
                    ChatId = chat.ChatId,
                    ParameterId = P("OverallComment"),
                    WeekStartUtc = weekStartUtc,
                    WeekEndUtc = weekEndUtc,
                    ParameterScore = null,
                    ParameterContent = comment!,
                    MentorId = chat.MentorId,
                    StudentId = chat.StudentId,
                    SubjectRole = "Global",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);

        return Ok(new
        {
            runId = run.RunId,
            weekStartUtc,
            weekEndUtc,
            totalChats = transcripts.Count,
            alreadyAnalyzed = analyzedChatIds.Count,
            newlyAnalyzed = results.Count,
            skipped = analyzedChatIds.Count
        });
    }
    // Helpers para leer propiedades de forma tolerante (case-insensitive y con alias)
    private static decimal? Score(object obj, params string[] names)
    {
        var t = obj.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n, System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.Public |
                                     System.Reflection.BindingFlags.IgnoreCase);
            if (p is null) continue;
            var v = p.GetValue(obj);
            if (v is null) continue;
            try { return Convert.ToDecimal(v); } catch { /* sigue probando */ }
        }
        return null;
    }

    private static string? Text(object obj, params string[] names)
    {
        var t = obj.GetType();
        foreach (var n in names)
        {
            var p = t.GetProperty(n, System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.Public |
                                     System.Reflection.BindingFlags.IgnoreCase);
            if (p is null) continue;
            var v = p.GetValue(obj);
            if (v is string s && !string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }


    private static TimeZoneInfo TryFindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch
        {
            // fallback Windows <-> IANA común para Bogotá
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
    }
}
