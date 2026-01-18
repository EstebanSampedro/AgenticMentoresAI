using System.ComponentModel.DataAnnotations;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public class SearchMessagesRequest
{
    public string MentorEmail { get; set; } = "";
    public string Query { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
