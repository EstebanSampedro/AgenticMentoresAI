namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class HtmlProcessingResult
{
    public string CleanText { get; set; } = string.Empty;
    public List<string> Urls { get; set; } = new();
}
