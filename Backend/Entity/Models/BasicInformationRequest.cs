using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class BasicInformationRequest
{
    [JsonPropertyName("institutionalEmail")]
    public string InstitutionalEmail { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("identificationNumber")]
    public string IdentificationNumber { get; set; } = string.Empty;

    [JsonPropertyName("idBanner")]
    public string IdBanner { get; set; } = string.Empty;
}
