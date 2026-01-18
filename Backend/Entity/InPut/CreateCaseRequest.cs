using System.ComponentModel.DataAnnotations;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public sealed record CreateCaseRequest(
    [Required] string BannerStudent,
    [Required] string BannerMentor,
    [Required] string OwnerEmail,    
    [Required] string Summary,
    [Required] string Theme,
    [Required] DateOnly NextDate
);
