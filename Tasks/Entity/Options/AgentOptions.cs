namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Options;

public class AgentOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string GrantType { get; set; } = "password";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
