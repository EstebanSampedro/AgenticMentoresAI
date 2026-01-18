using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public sealed class SummaryRequest
{
    [JsonPropertyName("SummaryType")]
    public string? SummaryType { get; set; }
}
