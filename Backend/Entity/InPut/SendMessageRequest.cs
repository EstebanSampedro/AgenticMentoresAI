using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public class SendMessageRequest
{
    public string SenderRole { get; set; } = "Mentor";
    public string ContentType { get; set; } = "html"; // "html" o "text"
    public string? Content { get; set; }
    public List<AttachmentModel>? Attachments { get; set; }
}
