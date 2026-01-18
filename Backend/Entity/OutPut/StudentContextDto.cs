namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public class StudentContextDto
{
    public string? Identification { get; set; }
    public string? BannerId { get; set; }
    public string? FullName { get; set; }
    public string? FavoriteName { get; set; }
    public string? Career { get; set; }
    public string? Faculty { get; set; }
    public int CurrentSemester { get; set; }
    public string? Gender { get; set; }
    public string? MentorGender { get; set; }
}
