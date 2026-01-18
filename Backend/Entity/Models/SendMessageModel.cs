namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class SendMessageModel
{
    public string ChatId { get; set; }

    public string SenderRole { get; set; }

    public string Content { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public string Date { get; set; }
}
