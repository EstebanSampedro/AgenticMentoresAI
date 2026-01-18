using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class BannerWebApiTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName(".issued")]
    public string Issued { get; set; } = string.Empty;

    [JsonPropertyName(".expires")]
    public string Expires { get; set; } = string.Empty;
}
