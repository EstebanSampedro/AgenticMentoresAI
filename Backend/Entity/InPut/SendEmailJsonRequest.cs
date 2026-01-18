using System.ComponentModel.DataAnnotations;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public class SendEmailJsonRequest
{
    [Required, EmailAddress]
    public string To { get; set; } = default!;

    [Required, MaxLength(300)]
    public string Subject { get; set; } = default!;

    [Required]
    public string HtmlBody { get; set; } = default!;

    public Base64AttachmentDto? Attachment { get; set; }
}

public class Base64AttachmentDto
{
    [Required, MaxLength(200)]
    public string FileName { get; set; } = default!;

    [Required, MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    [Required]
    public string Base64 { get; set; } = default!;
}
