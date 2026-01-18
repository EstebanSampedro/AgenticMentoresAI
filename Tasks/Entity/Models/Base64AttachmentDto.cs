using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class Base64AttachmentDto
{
    /// <summary>
    /// Nombre del archivo, incluyendo extensión. Ej: "Certificado.pdf"
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = default!;

    /// <summary>
    /// Tipo de contenido MIME. Ej: "application/pdf", "image/png".
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// Contenido del archivo en Base64 (sin encabezado data:).
    /// </summary>
    [Required]
    public string Base64 { get; set; } = default!;
}

