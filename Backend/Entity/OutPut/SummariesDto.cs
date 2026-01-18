using Academikus.AgenteInteligenteMentoresWebApi.Entity.DTOs;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public class SummariesDto
{
    public IEnumerable<ChatSummaryDto> Summaries { get; set; } = new List<ChatSummaryDto>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public string? UrlNextPage { get; set; }
}
