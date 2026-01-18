namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Options;

public class BannerWebApiOptions
{
    public const string SectionName = "BannerWebApi";

    public string BaseUrl { get; set; } = string.Empty;
    public string GrantType { get; set; } = "password";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
