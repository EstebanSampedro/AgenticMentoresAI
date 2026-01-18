namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class EmailAttachmentModel
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/pdf"; // MIME
    public byte[] ContentBytes { get; set; } = Array.Empty<byte>();
}
