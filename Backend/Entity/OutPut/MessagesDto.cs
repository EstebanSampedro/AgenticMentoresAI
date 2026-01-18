using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public sealed class MessagesDto
{
    public List<ChatMessageModel> Messages { get; init; } = new();
    public string? UrlNextPage { get; init; }
    public int TotalCount { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
}
