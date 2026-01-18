namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public sealed record CreateCaseResponse(
    string? CaseId,
    string? SalesforceUrl,
    string? BannerStudent,
    string? BannerMentor,
    string? OwnerEmail,
    DateTime NextDateUtc,
    string? Status,
    string? Priority
);
