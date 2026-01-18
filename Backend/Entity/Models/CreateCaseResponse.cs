namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class CreateCaseResponse
{
    public string Id { get; set; } = "";
    public bool Success { get; set; }
    public List<object> Errors { get; set; } = new();
}
