namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public class NewMessageDto
{
    public string ChatId { get; set; }
    public string MessageId { get; set; }
    public string SenderRole { get; set; }
    public string Content { get; set; }
    public string ContentType { get; set; }
    public string Timestamp { get; set; }
    public bool IAEnabled { get; set; }
}
