namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public partial class ChatModel
{
    public int Id { get; set; }

    public int MentorId { get; set; }

    public int StudentId { get; set; }

    public bool IaEnabled { get; set; }
    public virtual ICollection<ConversationModel> Conversations { get; set; } = new List<ConversationModel>();

    public virtual UserModel Mentor { get; set; } = null!;

    public virtual UserModel Student { get; set; } = null!;
}
