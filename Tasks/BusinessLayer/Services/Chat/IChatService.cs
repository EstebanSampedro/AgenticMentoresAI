using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.OutPut;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Chat;

public interface IChatService
{
    Task<bool> ChatExistsForUserAsync(string msTeamsChatId, string senderEntraUserId);

    Task<ChatIdAssignmentResultDto> UpdateChatMsTeamsIdAsync(
        string senderEntraId,
        string msTeamsChatId);

    Task MarkChatAsUnreadIfStudentAsync(Message messageEntity);

    Task<bool?> GetAiEnabledStateAsync(string msTeamsChatId);

    Task<(string? MentorBannerId, string? StudentBannerId, string? MentorEmail)?>
        GetBannerIdsForChatAsync(string msTeamsChatId);

    Task<List<string>> GetChatsWithMessagesInWindowAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct);

    Task<int?> GetChatIdAsync(string msTeamsChatId);

    Task<bool> IsAiEnabledForChatAsync(string chatId);

    Task<string?> GetStudentEmailByChatIdAsync(string chatId);

    Task UpdateLastAiBatchDateAsync(string chatId);
}
