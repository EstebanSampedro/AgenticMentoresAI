using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public class SendEmailFormRequest
{
    [Required, EmailAddress]
    public string To { get; set; } = default!;

    [Required, MaxLength(300)]
    public string Subject { get; set; } = default!;

    [Required]
    public string HtmlBody { get; set; } = default!;

    [Required]
    public IFormFile File { get; set; } = default!;
}
