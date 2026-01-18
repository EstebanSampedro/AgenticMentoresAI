using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.DailySummaries;

public interface ISummaryService
{
    Task ExecuteProcessorAsync(TimeZoneInfo tz, CancellationToken ct);

    Task<int?> GetChatIdAsync(string chatMSTeamsId);

    Task SaveSummaryEntityAsync(Summary summary);

    Task SaveSummaryAsync(string chatId, string summaryType, SummaryApiResponse summaryResponse);
}
