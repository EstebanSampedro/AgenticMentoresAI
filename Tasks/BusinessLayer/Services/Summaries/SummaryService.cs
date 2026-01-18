using Academikus.AgenteInteligenteMentoresTareas.Business.Common.Time;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Chat;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.DailySummaries;

public class SummaryService : ISummaryService
{
    private readonly DBContext _context;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly IConfiguration _configuration;
    private readonly IChatService _chatService;
    private readonly IBackendApiClientService _backendApiClient;

    public SummaryService(
        DBContext context,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        IConfiguration configuration,
        IChatService chatService,
        IBackendApiClientService backendApiClient)
    {
        _context = context;
        _serviceAccount = serviceAccountOptions.Value;
        _configuration = configuration;
        _chatService = chatService;
        _backendApiClient = backendApiClient;
    }

    public async Task ExecuteProcessorAsync(TimeZoneInfo tz, CancellationToken ct)
    {
        // Día objetivo = AYER en hora local Ecuador
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var yesterdayLocalDate = nowLocal.Date.AddDays(-1);
        var targetDayLocal = DateOnly.FromDateTime(yesterdayLocalDate);

        // Ventana UTC del día local [startUtc, endUtc)
        var (startUtc, endUtc) = TimeZoneHelper.GetUtcWindowForLocalDay(targetDayLocal, tz);

        Console.WriteLine($"DailySummaries: generando para {targetDayLocal:yyyy-MM-dd} " +
                          $"(UTC {startUtc:O} - {endUtc:O})");

        // Chats con mensajes AYER (evita resúmenes vacíos)
        var chatIds = await _chatService.GetChatsWithMessagesInWindowAsync(startUtc, endUtc, ct);

        if (chatIds.Count == 0)
        {
            Console.WriteLine($"DailySummaries: no hay chats con mensajes el {targetDayLocal:yyyy-MM-dd}.");
            return;
        }

        Console.WriteLine($"DailySummaries: {chatIds.Count} chats a procesar");

        var maxConc = Math.Max(1, _configuration.GetValue("DailySummaries:MaxConcurrency", 4));
        using var sem = new SemaphoreSlim(maxConc);
        var tasks = new List<Task>(chatIds.Count);

        foreach (var chatId in chatIds)
        {
            await sem.WaitAsync(ct);
            tasks.Add(ProcessChatAsync(chatId, startUtc, endUtc, targetDayLocal, tz, sem, ct));
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"DailySummaries: finalizado para {targetDayLocal:yyyy-MM-dd}");
    }

    private async Task ProcessChatAsync(
        string chatId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        DateOnly targetDayLocal,
        TimeZoneInfo tz,
        SemaphoreSlim sem,
        CancellationToken ct)
    {
        try
        {
            var exists = await ExistsDailySummaryAsync(chatId, startUtc.AddDays(1), endUtc.AddDays(1));
            if (exists)
            {
                Console.WriteLine($"DailySummaries: ya existe resumen para chat {chatId} en {targetDayLocal:yyyy-MM-dd}. Se omite.");
                return;
            }

            var bannerInfo = await _chatService.GetBannerIdsForChatAsync(chatId);
            if (bannerInfo is null)
            {
                Console.WriteLine($"DailySummaries: no se encontraron usuarios para el chat {chatId}.");
                return;
            }

            (string? mentorBannerId, string? studentBannerId, string? mentorEmail) = bannerInfo.Value;

            var (ok, summary, error) = await _backendApiClient.CreateDailySummaryAsync(chatId);
            if (!ok)
            {
                Console.WriteLine($"DailySummaries: fallo al crear resumen para {chatId}. Error: {error}");
                return;
            }

            // Si el backend indica específicamente que no hay contenido para generar resumen
            if (summary?.Code == 2)
            {
                Console.WriteLine($"DailySummaries: {summary.Message}");
                return;
            }

            if (summary?.Data == null ||
                string.IsNullOrWhiteSpace(summary.Data.Summary) ||
                string.IsNullOrWhiteSpace(summary.Data.Theme))
            {
                Console.WriteLine($"DailySummaries: contenido de resumen inválido para {chatId}");
                return;
            }

            var priority = summary.Data.Priority?.Trim().ToLowerInvariant() ?? "baja";

            string caseStatus = priority switch
            {
                "alta" or "media" => "No Atendido",
                _ => "Atendido"
            };

            var nextDateLocal = ComputeNextDateEc(default, tz);

            if (string.IsNullOrWhiteSpace(studentBannerId) ||
                string.IsNullOrWhiteSpace(mentorBannerId) ||
                string.IsNullOrWhiteSpace(mentorEmail))
            {
                Console.WriteLine($"DailySummaries: Banner IDs o correo faltantes para chat {chatId}. No se crea caso.");
                return;
            }

            // Agregar llamado a Salesforce
            var (okCase, caseData, caseError) = await _backendApiClient.CreateCaseAsync(
                bannerStudent: studentBannerId,
                bannerMentor: mentorBannerId,
                ownerEmail: mentorEmail,
                summary: summary.Data.Summary,
                theme: summary.Data.Theme,
                nextDate: nextDateLocal
            );

            if (!okCase)
            {
                Console.WriteLine($"DailySummaries: ERROR creando caso para chat {chatId}. Detalle: {caseError}");
                return;
            }

            Console.WriteLine(
                $"DailySummaries: caso creado correctamente. " +
                $"CaseId={caseData?.CaseId}, " +
                $"Url={caseData?.SalesforceUrl}, " +
                $"Status={caseData?.Status}, " +
                $"Priority={caseData?.Priority}"
            );

            Console.WriteLine($"DailySummaries: resumen OK para {chatId} - Priority={priority} Estado caso={caseStatus}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DailySummaries: error procesando chat {chatId}. {ex}");
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task SaveSummaryAsync(
    string chatId,
    string summaryType,
    SummaryApiResponse? summaryResponse)
    {
        if (string.IsNullOrWhiteSpace(chatId) ||
            summaryResponse is null ||
            !summaryResponse.Success ||
            summaryResponse.Data?.Summary is null)
            return;

        var chatDbId = await GetChatIdAsync(chatId);

        if (chatDbId is null)
        {
            Console.WriteLine($"[Summary] Chat no encontrado: {chatId}");
            return;
        }

        var payload = summaryResponse.Data.Summary;

        string? keyPointsText = null;
        if (payload.KeyPoints?.Any() == true)
        {
            keyPointsText = string.Join("; ",
                payload.KeyPoints
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim().TrimEnd('.').Replace(";", ",")));
        }

        var summaryEntity = new Summary
        {
            ChatId = chatDbId.Value,
            Summary1 = (payload.Overview ?? string.Empty).Trim(),
            KeyPoints = keyPointsText,
            SummaryType = summaryType,
            EscalationReason = payload.EscalationReason?.Trim(),
            Theme = payload.Theme?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _serviceAccount.Email
        };

        await SaveSummaryEntityAsync(summaryEntity);
    }


    public async Task SaveSummaryEntityAsync(Summary summary)
    {
        _context.Summaries.Add(summary);
        await _context.SaveChangesAsync();
    }

    public async Task<int?> GetChatIdAsync(string chatMSTeamsId)
    {
        return await _context.Chats
            .Where(c => c.MsteamsChatId == chatMSTeamsId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<bool> ExistsDailySummaryAsync(
        string chatMSTeamsId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        var chatId = await _chatService.GetChatIdAsync(chatMSTeamsId);
        if (chatId is null) 
            return false;

        // Deduplicación por ventana UTC del día local objetivo
        return await _context.Summaries.AsNoTracking()
            .AnyAsync(s => s.ChatId == chatId
                        && s.CreatedAt >= startUtc && s.CreatedAt < endUtc
                        && s.SummaryType == "Diario");
    }

    private DateOnly ComputeNextDateEc(DateOnly _, TimeZoneInfo tzEc)
    {
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzEc);
        var sevenDaysLaterLocal = nowLocal.AddDays(7).Date;
        return DateOnly.FromDateTime(sevenDaysLaterLocal);
    }    
}
