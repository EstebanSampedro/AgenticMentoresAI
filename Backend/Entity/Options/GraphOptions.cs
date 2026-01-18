namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;

public class GraphOptions
{
    public string? Scopes { get; set; } = "";
    public int AccessTokenCacheMinutes { get; set; } = 5;
    public string? SenderEmail { get; set; } = "";
}
