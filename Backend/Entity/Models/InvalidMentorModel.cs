namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class InvalidMentorModel
{
    public string Email { get; set; }
    public string FullName { get; set; }
    public string MissingFields { get; set; } // Campos inválidos separados por coma
}
