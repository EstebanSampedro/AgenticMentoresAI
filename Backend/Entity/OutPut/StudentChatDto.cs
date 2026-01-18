namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public class StudentChatDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool AIState { get; set; }
    public string AIChangeReason { get; set; } = string.Empty;
    public DateTime? LastMessageDate { get; set; }
    public string LastMessageContent { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}
