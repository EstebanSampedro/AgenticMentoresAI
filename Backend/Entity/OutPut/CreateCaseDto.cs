namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public sealed record CreateCaseDto(
    string? CaseId,
    string? SalesforceUrl,
    string? BannerStudent,
    string? BannerMentor,
    string? OwnerEmail,
    DateOnly NextDate,
    string? Status,
    string? Priority
);
