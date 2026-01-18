namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class GeneralSalesforceUserResponse
{
    public int TotalSize { get; set; }
    public bool Done { get; set; }
    public List<MentorResponse> Records { get; set; }
}
