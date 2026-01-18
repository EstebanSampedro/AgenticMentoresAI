namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public sealed record LastCaseDto(
    string? Id,
    string? CaseNumber,
    string? Subject,
    string? Status,
    string? Priority,
    string? Origin,
    string? OwnerName,
    string? OwnerEmail,
    DateTimeOffset? CreatedDate,
    DateTimeOffset? LastModifiedDate,
    string? SalesforceUrl,
    string? QueryBannerId
);
