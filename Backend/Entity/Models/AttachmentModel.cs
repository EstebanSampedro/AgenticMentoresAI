namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class AttachmentModel
{
    public string? ContentUrl { get; set; }   // URL generada por el backend (CreateLink de Graph)
    public string? Name { get; set; }         // Nombre del archivo
    public string? ContentType { get; set; }  // MIME type (application/pdf, image/png, etc.)
    public string AttachmentType { get; set; } = "reference"; // "reference", "image", "file", etc.
    public string? DriveId { get; set; }
    public string? ItemId { get; set; }
    public string? ThumbnailUrl { get; set; }
    public long? Size { get; set; }
    public DateTime? LastModified { get; set; }
}
