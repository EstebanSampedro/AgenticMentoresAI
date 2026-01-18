namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public record SemesterInfo(
    string Name,
    string? BannerCode,
    string? BusinessLine,
    DateOnly StartDate,
    DateOnly EndDate
);
