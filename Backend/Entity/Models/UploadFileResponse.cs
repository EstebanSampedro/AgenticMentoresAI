namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class UploadFileResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FileUrl { get; set; } // Link compartido (view) para usar como ContentUrl en attachment
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string DriveId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public long Size { get; set; }
}
