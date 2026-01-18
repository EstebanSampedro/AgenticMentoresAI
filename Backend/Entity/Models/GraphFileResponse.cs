namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public sealed class GraphFileResponse
{
    public Stream? Stream { get; init; }
    public byte[]? Content { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = "file";
}
