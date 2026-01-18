using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public record SalesforceCase
{
    [JsonProperty("Id")] public string? Id { get; init; }
    [JsonProperty("CaseNumber")] public string? CaseNumber { get; init; }
    [JsonProperty("Subject")] public string? Subject { get; init; }
    [JsonProperty("Status")] public string? Status { get; init; }
    [JsonProperty("Priority")] public string? Priority { get; init; }
    [JsonProperty("Origin")] public string? Origin { get; init; }
    [JsonProperty("Owner")] public SalesforceOwnerRef? Owner { get; init; }
    [JsonProperty("CreatedDate")] public DateTimeOffset? CreatedDate { get; init; }
    [JsonProperty("LastModifiedDate")] public DateTimeOffset? LastModifiedDate { get; init; }

    [JsonIgnore] public string? OwnerName => Owner?.Name;
    [JsonIgnore] public string? OwnerEmail => Owner?.Email;
}

public record SalesforceOwnerRef
{
    [JsonProperty("Name")] public string? Name { get; init; }
    [JsonProperty("Email")] public string? Email { get; init; }
}
