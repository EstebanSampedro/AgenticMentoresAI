namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public class SearchMessagesDto
{
    public int Id { get; set; }
    public string SenderRole { get; set; }
    public string StudentFullName { get; set; }
    public string ChatId { get; set; }
    public string Content { get; set; }
    public string ContentType { get; set; }
    public DateTime? Date { get; set; }
}
