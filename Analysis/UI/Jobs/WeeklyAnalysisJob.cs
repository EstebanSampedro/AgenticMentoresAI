using Academikus.AnalysisMentoresVerdes.Business.Abstractions;
using Academikus.AnalysisMentoresVerdes.Data.Ef;
using Academikus.AnalysisMentoresVerdes.Entity.Analysis;
using Academikus.AnalysisMentoresVerdes.Entity.Options;
using Academikus.AnalysisMentoresVerdes.Proxy.AzureOpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Academikus.AnalysisMentoresVerdes.WebApi.Jobs;

/// <summary>
/// Servicio en segundo plano que ejecuta el análisis semanal cada domingo a las 11pm (hora Ecuador).
/// </summary>
public sealed class WeeklyAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WeeklyAnalysisBackgroundService> _log;
    private readonly IOptions<AnalysisOptions> _opts;
    private readonly TimeZoneInfo _ecuadorTz;

    public WeeklyAnalysisBackgroundService(
        IServiceProvider services,
        ILogger<WeeklyAnalysisBackgroundService> log,
        IOptions<AnalysisOptions> opts)
    {
        _services = services;
        _log = log;
        _opts = opts;

        // Ecuador = America/Guayaquil o Bogota (mismo offset UTC-5)
        _ecuadorTz = FindEcuadorTimeZone();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("WeeklyAnalysisBackgroundService iniciado. Próxima ejecución: {Next}",
            GetNextSundayAt23(DateTime.UtcNow));

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = GetNextSundayAt23(now);
            var delay = nextRun - now;

            if (delay.TotalMilliseconds > 0)
            {
                _log.LogInformation("Esperando hasta {NextRun} para ejecutar análisis semanal (en {Hours}h {Minutes}m)",
                    nextRun, (int)delay.TotalHours, delay.Minutes);

                await Task.Delay(delay, stoppingToken);
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            // Ejecutar análisis con reintentos
            await ExecuteWeeklyAnalysisWithRetry(stoppingToken);

            // Esperar 1 minuto para evitar ejecución doble
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _log.LogInformation("WeeklyAnalysisBackgroundService detenido.");
    }

    private async Task ExecuteWeeklyAnalysisWithRetry(CancellationToken ct)
    {
        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            attempt++;

            try
            {
                _log.LogInformation(" Iniciando análisis semanal automático (intento {Attempt}/{Max})",
                    attempt, maxRetries);

                using var scope = _services.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<AnalysisDbContext>();
                var ai = scope.ServiceProvider.GetRequiredService<IGenerativeClient>();

                // Calcular ventana de la semana anterior
                var window = CalculatePreviousWeekWindow();

                _log.LogInformation(" Ventana de análisis: {Start} -> {End} (UTC)",
                    window.WeekStartUtc, window.WeekEndUtc);

                // Ejecutar análisis (basado en tu endpoint analyze-save)
                var result = await ExecuteAnalysisAsync(db, ai, window, ct);

                _log.LogInformation(
                    " Análisis completado exitosamente. RunId={RunId}, Chats analizados: {New}/{Total}",
                    result.RunId, result.NewlyAnalyzed, result.TotalChats);

                // Éxito - salir del bucle de reintentos
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    " Error en análisis semanal (intento {Attempt}/{Max}): {Message}",
                    attempt, maxRetries, ex.Message);

                if (attempt >= maxRetries)
                {
                    _log.LogCritical(
                        " Análisis semanal falló después de {Max} intentos. Requiere intervención manual.",
                        maxRetries);
                    return;
                }

                // Esperar antes de reintentar (backoff exponencial)
                var waitTime = TimeSpan.FromMinutes(Math.Pow(2, attempt));
                _log.LogWarning("⏳ Reintentando en {Minutes} minutos...", waitTime.TotalMinutes);
                await Task.Delay(waitTime, ct);
            }
        }
    }

    private async Task<AnalysisResult> ExecuteAnalysisAsync(
        AnalysisDbContext db,
        IGenerativeClient ai,
        WeeklyWindow window,
        CancellationToken ct)
    {
        // 1️⃣ Obtener TODOS los transcripts de la ventana (sin límite de take)
        var transcripts = await FetchAllTranscriptsAsync(db, window, ct);

        if (transcripts.Count == 0)
        {
            _log.LogWarning("⚠️ No se encontraron transcripts para la ventana especificada");
            return new AnalysisResult { TotalChats = 0, NewlyAnalyzed = 0, RunId = 0 };
        }

        _log.LogInformation("📊 Encontrados {Count} chats en total", transcripts.Count);

        // 2️⃣ Filtrar los que YA fueron analizados
        var analyzedChatIds = await db.AnalysisSummaries
            .Where(s => s.WeekStartUtc == window.WeekStartUtc && s.WeekEndUtc == window.WeekEndUtc)
            .Select(s => s.ChatId)
            .ToHashSetAsync(ct);

        var toAnalyze = transcripts
            .Where(t => !analyzedChatIds.Contains(t.ChatId))
            .ToList();

        if (toAnalyze.Count == 0)
        {
            _log.LogInformation("ℹ️ Todos los chats ya fueron analizados previamente");
            return new AnalysisResult
            {
                TotalChats = transcripts.Count,
                NewlyAnalyzed = 0,
                AlreadyAnalyzed = analyzedChatIds.Count,
                RunId = 0
            };
        }

        _log.LogInformation(
            "🔍 Analizando {New} chats nuevos (omitiendo {Existing} ya analizados)",
            toAnalyze.Count, analyzedChatIds.Count);

        // 3️⃣ Analizar cada chat con Azure OpenAI
        var results = new List<(ChatTranscript chat, dynamic aiResult)>();

        foreach (var transcript in toAnalyze)
        {
            try
            {
                var aiResult = await ai.AnalyzeAsync(transcript, ct);
                results.Add((transcript, aiResult));
                _log.LogDebug("✓ Chat {ChatId} analizado", transcript.ChatId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error analizando ChatId={ChatId}", transcript.ChatId);
                throw; // Re-lanzar para activar reintentos
            }
        }

        // 4️⃣ Persistir resultados en BD
        await using var trx = await db.Database.BeginTransactionAsync(ct);

        // Obtener o crear Run
        var run = await db.AnalysisRuns
            .SingleOrDefaultAsync(x =>
                x.WeekStartUtc == window.WeekStartUtc &&
                x.WeekEndUtc == window.WeekEndUtc, ct);

        if (run is null)
        {
            run = new AnalysisRun
            {
                WeekStartUtc = window.WeekStartUtc,
                WeekEndUtc = window.WeekEndUtc,
                Status = "Completed",
                ModelVersion = _opts.Value.ModelVersion ?? "gpt-4o-mini",
                PromptVersion = _opts.Value.PromptVersion ?? "v1",
                CreatedAt = DateTime.UtcNow
            };
            db.AnalysisRuns.Add(run);
            await db.SaveChangesAsync(ct);
        }

        // Asegurar parámetros base
        await EnsureParametersAsync(db, ct);
        var paramDict = await db.AnalysisParameterDefinitions
            .ToDictionaryAsync(x => x.Code, x => x.ParameterId, ct);

        // Insertar Summaries y Observations
        foreach (var (chat, aiResult) in results)
        {
            db.AnalysisSummaries.Add(new AnalysisSummary
            {
                RunId = run.RunId,
                ChatId = chat.ChatId,
                WeekStartUtc = window.WeekStartUtc,
                WeekEndUtc = window.WeekEndUtc,
                RawJson = JsonSerializer.Serialize(aiResult),
                CreatedAt = DateTime.UtcNow
            });

            // Crear observations para cada parámetro
            AddObservations(db, run.RunId, chat, aiResult, window, paramDict);
        }

        await db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);

        return new AnalysisResult
        {
            RunId = run.RunId,
            TotalChats = transcripts.Count,
            NewlyAnalyzed = results.Count,
            AlreadyAnalyzed = analyzedChatIds.Count
        };
    }

    private async Task<List<ChatTranscript>> FetchAllTranscriptsAsync(
        AnalysisDbContext db,
        WeeklyWindow window,
        CancellationToken ct)
    {
        // Consulta SQL sin límite de TOP
        const string sql = @"
            SELECT 
                c.Id AS ChatId,
                c.MentorId,
                c.StudentId,
                m.CreatedAt,
                m.SenderRole,
                m.MessageContent
            FROM dbo.Chat c
            JOIN dbo.Conversation cv ON cv.ChatId = c.Id
            JOIN dbo.Message m ON m.ConversationId = cv.Id
            WHERE m.CreatedAt >= @startUtc AND m.CreatedAt < @endUtc
            ORDER BY c.Id, m.CreatedAt;";

        var rows = new List<(long chatId, long? mentorId, long? studentId, DateTime createdAt, string role, string text)>();

        var cs = db.Database.GetDbConnection().ConnectionString;
        await using (var conn = new Microsoft.Data.SqlClient.SqlConnection(cs))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@startUtc", window.WeekStartUtc);
            cmd.Parameters.AddWithValue("@endUtc", window.WeekEndUtc);

            await using var rdr = await cmd.ExecuteReaderAsync(ct);

            while (await rdr.ReadAsync(ct))
            {
                var chatId = (long)rdr.GetInt32(0);
                var mentorId = rdr.IsDBNull(1) ? (long?)null : (long)rdr.GetInt32(1);
                var studentId = rdr.IsDBNull(2) ? (long?)null : (long)rdr.GetInt32(2);
                var createdAt = rdr.GetDateTime(3);
                var role = rdr.IsDBNull(4) ? "" : rdr.GetString(4);
                var text = rdr.IsDBNull(5) ? "" : rdr.GetString(5);

                rows.Add((chatId, mentorId, studentId, createdAt, role, text));
            }
        }

        // Agrupar por chat
        var transcripts = rows
            .GroupBy(r => new { r.chatId, r.mentorId, r.studentId })
            .Select(g =>
            {
                var turns = g.Select(r =>
                {
                    var (mappedRole, ia) = MapRole(r.role);
                    return new ChatMessageTurn(
                        CreatedAtUtc: r.createdAt,
                        SenderRole: mappedRole,
                        Text: r.text,
                        IAEnabled: ia
                    );
                })
                .OrderBy(t => t.CreatedAtUtc)
                .ToList();

                return new ChatTranscript(
                    ChatId: g.Key.chatId,
                    MentorId: g.Key.mentorId,
                    StudentId: g.Key.studentId,
                    Turns: turns
                );
            })
            .ToList();

        return transcripts;
    }

    private static (string role, bool ia) MapRole(string raw)
    {
        var s = (raw ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "ia" => ("IA", true),
            "estudiante" => ("Estudiante", false),
            "mentor" => ("Mentor", false),
            _ => (raw ?? "", false)
        };
    }

    private async Task EnsureParametersAsync(AnalysisDbContext db, CancellationToken ct)
    {
        var required = new (string Code, string Name, decimal Min, decimal Max, string? Desc)[]
        {
            ("MisunderstoodPct", "Porcentaje de malentendidos", 0, 100, "Porcentaje estimado de malentendidos"),
            ("EmpathyAI", "Empatía IA", 0, 10, null),
            ("EmpathyMentor", "Empatía Mentor", 0, 10, null),
            ("SentimentStudentStart", "Sentimiento inicio estudiante", 0, 0, "Texto libre con el resumen"),
            ("SentimentStudentEnd", "Sentimiento fin estudiante", 0, 0, "Texto libre con el resumen"),
            ("EmotionAvg", "Promedio emoción", 0, 10, null),
            ("WarmthAI", "Calidez IA", 0, 10, null),
            ("WarmthMentor", "Calidez Mentor", 0, 10, null),
            ("OverallComment", "Comentario global", 0, 0, "Texto libre con el resumen"),
            ("SatisfiedUser", "Usuario satisfecho", 0, 0, "Indica si la solicitud del usuario fue resuelta (true/false)"),
            ("Issue", "Tema principal", 0, 0, "Tema tratado: Pregunta frecuente o Justificación de inasistencia")
        };

        var existing = await db.AnalysisParameterDefinitions
            .AsNoTracking()
            .ToListAsync(ct);

        var byCode = existing.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var p in required)
        {
            if (!byCode.ContainsKey(p.Code))
            {
                db.AnalysisParameterDefinitions.Add(new AnalysisParameterDefinition
                {
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Desc,
                    MinScore = p.Min,
                    MaxScore = p.Max,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private void AddObservations(
        AnalysisDbContext db,
        long runId,
        ChatTranscript chat,
        dynamic aiResult,
        WeeklyWindow window,
        Dictionary<string, int> paramDict)
    {
        var observations = new List<AnalysisObservation>
        {
            CreateObservation(runId, chat, window, paramDict["MisunderstoodPct"],
                Score(aiResult, "MisunderstoodPct"), null, "Global"),

            CreateObservation(runId, chat, window, paramDict["EmpathyAI"],
                Score(aiResult, "EmpathyAI", "EmpathyIa", "Empathy"), null, "IA"),

            CreateObservation(runId, chat, window, paramDict["EmpathyMentor"],
                Score(aiResult, "EmpathyMentor"), null, "Mentor"),

            CreateObservation(runId, chat, window, paramDict["SentimentStudentStart"],
                null, Text(aiResult, "SentimentStudentStart", "sentiment_student_start"), "Estudiante"),

            CreateObservation(runId, chat, window, paramDict["SentimentStudentEnd"],
                null, Text(aiResult, "SentimentStudentEnd", "sentiment_student_end"), "Estudiante"),

            CreateObservation(runId, chat, window, paramDict["EmotionAvg"],
                Score(aiResult, "EmotionAvg"), null, "Global"),

            CreateObservation(runId, chat, window, paramDict["WarmthAI"],
                Score(aiResult, "WarmthAI", "WarmthIa", "Warmth"), null, "IA"),

            CreateObservation(runId, chat, window, paramDict["WarmthMentor"],
                Score(aiResult, "WarmthMentor"), null, "Mentor"),

            CreateObservation(runId, chat, window, paramDict["SatisfiedUser"],
                null, Text(aiResult, "SatisfiedUser"), "Global"),

            CreateObservation(runId, chat, window, paramDict["Issue"],
                null, Text(aiResult, "Issue"), "Global")
        };

        var comment = Text(aiResult, "OverallComment", "Overall", "Comment", "Resumen");
        if (!string.IsNullOrWhiteSpace(comment))
        {
            observations.Add(CreateObservation(runId, chat, window,
                paramDict["OverallComment"], null, comment, "Global"));
        }

        db.AnalysisObservations.AddRange(observations);
    }

    private AnalysisObservation CreateObservation(
        long runId,
        ChatTranscript chat,
        WeeklyWindow window,
        int parameterId,
        decimal? score,
        string? content,
        string subjectRole)
    {
        return new AnalysisObservation
        {
            RunId = runId,
            ChatId = chat.ChatId,
            ParameterId = parameterId,
            ParameterScore = score,
            ParameterContent = content,
            WeekStartUtc = window.WeekStartUtc,
            WeekEndUtc = window.WeekEndUtc,
            MentorId = chat.MentorId,
            StudentId = chat.StudentId,
            SubjectRole = subjectRole,
            CreatedAt = DateTime.UtcNow
        };
    }

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
            try { return Convert.ToDecimal(v); } catch { }
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

    private WeeklyWindow CalculatePreviousWeekWindow()
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _ecuadorTz);
        var startThisWeekLocal = nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek); // domingo 00:00
        var startPrevLocal = startThisWeekLocal.AddDays(-7);
        var endPrevLocal = startThisWeekLocal;

        return new WeeklyWindow(
            TimeZoneInfo.ConvertTimeToUtc(startPrevLocal, _ecuadorTz),
            TimeZoneInfo.ConvertTimeToUtc(endPrevLocal, _ecuadorTz));
    }
    //private WeeklyWindow CalculatePreviousWeekWindow()
    //{
    //    var zone = _ecuadorTz;

    //    var startLocal = new DateTime(2025, 12, 03, 16, 30, 00); // 16h30
    //    var endLocal = new DateTime(2025, 12, 02, 17, 05, 00); // 17h05

    //    return new WeeklyWindow(
    //        TimeZoneInfo.ConvertTimeToUtc(startLocal, zone),
    //        TimeZoneInfo.ConvertTimeToUtc(endLocal, zone));
    //}

    private DateTime GetNextSundayAt23(DateTime nowUtc)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _ecuadorTz);

        // Calcular próximo domingo
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)nowLocal.DayOfWeek + 7) % 7;
        if (daysUntilSunday == 0 && nowLocal.Hour >= 23)
            daysUntilSunday = 7; // Si ya pasó las 11pm del domingo actual, ir al siguiente

        var nextSunday = nowLocal.Date.AddDays(daysUntilSunday).AddHours(23);
        //var nextSunday = nowLocal.AddMinutes(5); //Para pruebas rápidas
        return TimeZoneInfo.ConvertTimeToUtc(nextSunday, _ecuadorTz);
    }

    private TimeZoneInfo FindEcuadorTimeZone()
    {
        try
        {
            // IANA (Linux/Mac)
            return TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
        }
        catch
        {
            try
            {
                // Windows
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch
            {
                _log.LogWarning("No se pudo encontrar timezone de Ecuador, usando UTC-5 personalizado");
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Ecuador",
                    TimeSpan.FromHours(-5),
                    "Ecuador Time",
                    "ECT");
            }
        }
    }

    private class AnalysisResult
    {
        public long RunId { get; set; }
        public int TotalChats { get; set; }
        public int NewlyAnalyzed { get; set; }
        public int AlreadyAnalyzed { get; set; }
    }
}