using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public record SalesforceQuery<T>
{
    [JsonProperty("TotalSize")]
    public int TotalSize { get; init; }

    [JsonProperty("Done")]
    public bool Done { get; init; }

    [JsonProperty("Records")]
    public List<T> Records { get; init; } = new();
}

public record SalesforceObjectAttributes
{
    [JsonProperty("Type")]
    public string Type { get; init; } = default!;

    [JsonProperty("Url")]
    public string Url { get; init; } = default!;
}
