using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Users;

public interface IUserService
{
    Task<List<UserTable>> GetLocalUsersAsync(string role, string type);    

    Task UpdateStudentBannerFieldsAsync(
        string studentEmail,
        string? bannerId,
        string? pidm,
        string? identification,
        string? career);

    Task<string> GetUserRoleAsync(Data.DB.EF.VirtualMentorDB.Entities.Chat chat, string senderEntraUserId);

    Task<UserTable?> GetStudentByEntraIdAsync(string entraUserId);

    Task<(int processed, int updated)> SyncStudentInfoAsync(
        CancellationToken cancellationToken);

    Task<List<string>> GetActiveMentorEntraIdsAsync();

    Task<List<string>> GetInactiveMentorEntraIdsAsync();
}
