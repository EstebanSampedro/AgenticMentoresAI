namespace Academikus.AgenteInteligenteMentoresTareas.Entity.OutPut;

public class ChatIdAssignmentResultDto
{
    public int ChatDbId { get; set; }
    public int StudentDbId { get; set; }
    public string MentorEntraId { get; set; } = string.Empty;
    public string MsTeamsChatId { get; set; } = string.Empty;
}
