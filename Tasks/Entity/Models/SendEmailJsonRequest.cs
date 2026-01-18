using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class SendEmailJsonRequest
{
    /// <summary>
    /// Dirección de correo del destinatario.
    /// </summary>
    [Required]
    [EmailAddress]
    public string To { get; set; } = default!;

    /// <summary>
    /// Asunto del correo electrónico.
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = default!;

    /// <summary>
    /// Cuerpo del correo en formato HTML.
    /// </summary>
    [Required]
    public string HtmlBody { get; set; } = default!;

    /// <summary>
    /// Lista de archivos adjuntos en Base64.
    /// </summary>
    [MinLength(1)]
    public Base64AttachmentDto Attachment { get; set; } = new();
}

