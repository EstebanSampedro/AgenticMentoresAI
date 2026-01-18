namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class MessageAttachmentItem
{
    public string FileName { get; set; } = "";
    public string FileType { get; set; } = "";          // "image", "pdf", "doc", etc.
    public string ContentType { get; set; } = "application/octet-stream";
    public string DownloadUrl { get; set; } = "";
    public string SourceType { get; set; } = "attachment";
}
