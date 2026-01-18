using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Email;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

/// <summary>
/// Controlador encargado de exponer los endpoints para el envío de correos
/// electrónicos, ya sea mediante JSON con adjunto en Base64 o mediante
/// multipart/form-data usando <see cref="IFormFile"/>.
/// </summary>
/// <remarks>
/// Este controlador aplica validaciones adicionales sobre el cuerpo de la petición,
/// decodifica adjuntos, valida tamaños máximos permitidos y delega el envío real
/// al servicio <see cref="IEmailService"/>.
/// 
/// Requiere autenticación según la política "AppOrUser" y aplica el filtro de acceso
/// <see cref="UserAccessFilter"/> para validar contexto del usuario.
/// </remarks>
[ApiController]
[Route("api/email")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;

    /// <summary>
    /// Tamaño máximo permitido para el string Base64 del archivo adjunto.
    /// Equivale aproximadamente a 20–30 MB de archivo real según el padding Base64.
    /// </summary>
    private const int MAX_BASE64_LENGTH = 30_000_000;

    /// <summary>
    /// Inicializa el controlador inyectando el servicio de envío de correos.
    /// </summary>
    /// <param name="emailService">Servicio responsable de realizar el envío del correo.</param>
    public EmailController(
        IEmailService emailService)
    {
        _emailService = emailService;
    }

    // =====================================================================
    //  ENDPOINT: Envío mediante JSON con adjunto Base64
    // =====================================================================

    /// <summary>
    /// Envía un correo electrónico utilizando un adjunto codificado en Base64.
    /// </summary>
    /// <param name="request">Modelo que contiene destinatario, asunto, cuerpo HTML y adjunto Base64.</param>
    /// <returns>Un <see cref="IActionResult"/> indicando el resultado del envío.</returns>
    /// <response code="200">El correo fue enviado correctamente.</response>
    /// <response code="400">Datos inválidos o adjunto Base64 no válido.</response>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendEmailJson([FromBody] SendEmailJsonRequest request)
    {
        // Valida los atributos [Required] del modelo automáticamente
        if (!ModelState.IsValid) 
            return ValidationProblem(ModelState);

        // Validaciones manuales para evitar valores con solo espacios.
        if (string.IsNullOrWhiteSpace(request.To))
            return BadRequest("El campo 'To' es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest("El campo 'Subject' es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.HtmlBody))
            return BadRequest("El campo 'HtmlBody' es obligatorio.");

        if (request.Attachment is null)
            return BadRequest("El adjunto es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.Attachment.Base64))
            return BadRequest("El adjunto Base64 no puede estar vacío.");

        // Validación de tamaño máximo permitido para prevenir abusos e impactos en memoria.
        if (request.Attachment.Base64.Length > MAX_BASE64_LENGTH)
            return BadRequest("El archivo adjunto es demasiado grande.");

        try
        {
            // Intento de decodificación Base64; FormatException o ArgumentException indican datos corruptos.
            var bytes = Convert.FromBase64String(request.Attachment.Base64);

            var attachment = new EmailAttachmentModel
            {
                FileName = string.IsNullOrWhiteSpace(request.Attachment.FileName)
                    ? "adjunto.bin"
                    : request.Attachment.FileName,

                ContentType = request.Attachment.ContentType,

                ContentBytes = bytes
            };

            // Normalización de los campos para evitar errores en Graph.
            var to = request.To.Trim();
            var subject = request.Subject.Trim();
            var body = request.HtmlBody;

            await _emailService.SendEmailAsync(
                to,
                subject,
                body,
                attachment);

            return Ok("Correo enviado con adjunto.");
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            // Ocurre cuando el Base64 tiene padding incorrecto, caracteres inválidos o longitud incorrecta.
            Console.WriteLine($"El adjunto no contiene un Base64 válido. {ex}");
            return BadRequest("El adjunto no contiene un Base64 válido.");
        }
        catch (Exception ex)
        {
            // Cualquier error inesperado (red, Graph, timeout, etc.)
            Console.WriteLine($"Error enviando correo (JSON). {ex}");
            return Problem("Ocurrió un error enviando el correo.");
        }
    }

    // =====================================================================
    //  ENDPOINT: Envío mediante multipart/form-data con IFormFile
    // =====================================================================

    /// <summary>
    /// Envía un correo electrónico utilizando un archivo adjunto recibido como multipart/form-data.
    /// </summary>
    /// <param name="form">Modelo que contiene destinatario, asunto, cuerpo HTML y el archivo adjunto.</param>
    /// <returns>Un <see cref="IActionResult"/> indicando el resultado del envío.</returns>
    /// <response code="200">El correo fue enviado correctamente.</response>
    /// <response code="400">El archivo adjunto no fue proporcionado o ocurrió una falla al procesarlo.</response>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("send-form")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendEmailForm([FromForm] SendEmailFormRequest form)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Validación del archivo adjunto físico.
        if (form.File is null || form.File.Length == 0)
            return BadRequest("Se requiere al menos un adjunto (file).");

        try
        {
            // Se lee el archivo completo en memoria.
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await form.File.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            var attachment = new EmailAttachmentModel
            {
                FileName = string.IsNullOrWhiteSpace(form.File.FileName) ? "adjunto.bin" : form.File.FileName,
                ContentType = string.IsNullOrWhiteSpace(form.File.ContentType) ? "application/octet-stream" : form.File.ContentType,
                ContentBytes = bytes
            };

            await _emailService.SendEmailAsync(
                form.To,
                form.Subject,
                form.HtmlBody,
                attachment);

            return Ok("Correo enviado con adjunto.");
        }
        catch (Exception ex)
        {
            // Gestión de cualquier fallo inesperado (Graph, IO, red, etc.)
            Console.WriteLine($"Error enviando correo (form-data). {ex}");
            return Problem("Ocurrió un error enviando el correo.");
        }
    }
}
