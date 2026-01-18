namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public partial class MessageModel
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public string SenderType { get; set; } = null!;

    public string MessageContent { get; set; } = null!;

    public string MessageContentType { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public virtual ConversationModel Conversation { get; set; } = null!;
}
