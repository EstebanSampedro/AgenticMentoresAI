namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class ExcelMentorModel
{
    public string FullName { get; set; }
    public string Email { get; set; }
    public string BannerId { get; set; }
    public string Pidm { get; set; }
    public string Identification { get; set; }
    public string Gender { get; set; }
    public string Role { get; set; }
    public string Type { get; set; }
    public string BackupEmail { get; set; }
    public string Status { get; set; }
    public VacationPeriodDto Vacation1 { get; set; }
    public VacationPeriodDto Vacation2 { get; set; }    
}

public class VacationPeriodDto
{
    public string Start { get; set; }
    public string End { get; set; }
}
