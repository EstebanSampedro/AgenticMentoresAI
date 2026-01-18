namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class SendMessageResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MessageId { get; set; }
    public string SentAt { get; set; }
}
