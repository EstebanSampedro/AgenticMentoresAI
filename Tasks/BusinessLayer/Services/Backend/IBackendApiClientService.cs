using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services;

public interface IBackendApiClientService
{
    Task<string> GetAppTokenAsync();

    Task<StudentBannerData?> GetStudentBannerDataAsync(string email);

    Task<(bool ok, DailySummaryApiResponse? summary, string? error)> CreateDailySummaryAsync(
        string chatId);

    Task<StudentContextModel?> GetStudentContextByEmailAsync(string email);

    Task<bool> SendMessageToChatAsync(string chatId, string htmlContent);

    Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string htmlBody, string fileUrl);

    Task<bool> UpdateAISettingsAsync(string chatId, bool aiState, string reason);

    Task<(bool ok, CreateCaseResponse? data, string? error)> CreateCaseAsync(
        string bannerStudent,
        string bannerMentor,
        string ownerEmail,
        string summary,
        string theme,
        DateOnly nextDate);
}
