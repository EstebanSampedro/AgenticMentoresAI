namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;

public class PagedResultDto<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<T> Results { get; set; } = new();
}
