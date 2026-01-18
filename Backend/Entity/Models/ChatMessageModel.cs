namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class ChatMessageModel
{
    public int Id { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public string MessageContentType { get; set; } = "html";
    public string Date { get; set; }
    public List<MessageAttachmentItem> Attachments { get; set; } = new();
}
