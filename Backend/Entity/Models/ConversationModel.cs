namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public partial class ConversationModel
{
    public int Id { get; set; }

    public int ChatId { get; set; }

    public bool Escalated { get; set; }

    public string? EscalatedReason { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? Summary { get; set; }

    public virtual ChatModel MentorStudent { get; set; } = null!;

    public virtual ICollection<MessageModel> Messages { get; set; } = new List<MessageModel>();
}
