namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public sealed class SearchMessagesPagedDto
{
    public IEnumerable<SearchMessagesItemDto> Results { get; set; } = Enumerable.Empty<SearchMessagesItemDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? UrlNextPage { get; set; }
}
