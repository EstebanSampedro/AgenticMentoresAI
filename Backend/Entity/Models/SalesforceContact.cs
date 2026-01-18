using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public record SalesforceContact
{
    [JsonProperty("Id")]
    public string Id { get; init; } = default!;

    [JsonProperty("Attributes")]
    public SalesforceObjectAttributes Attributes { get; init; } = new();

    [JsonProperty("Name")]
    public string Name { get; init; } = default!;

    [JsonProperty("Email")]
    public string? Email { get; init; }

    [JsonProperty("hed__UniversityEmail__c")]
    public string? UniversityEmail { get; init; }

    [JsonProperty("Codigo_banner__c")]
    public string? BannerId { get; init; }

    [JsonProperty("Asignacion__c")]
    public string? ProgramType { get; init; }

    [JsonProperty("Estudiantes_Asignados_Actualmente_Mentor__c")]
    public int? CurrentAssignedStudents { get; init; }

    [JsonProperty("Limite_Asignado__c")]
    public int? AssignedLimit { get; init; }
}
