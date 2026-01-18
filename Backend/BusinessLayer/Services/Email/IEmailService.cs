using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Email;

/// <summary>
/// Define las operaciones relacionadas con el envío de correos electrónicos
/// a través de un proveedor externo (como Microsoft Graph).
/// </summary>
/// <remarks>
/// Esta interfaz abstrae el envío de correos para permitir implementar
/// distintas soluciones (Graph, SMTP, servicios mock en pruebas, etc.).
/// </remarks>
public interface IEmailService
{
    /// <summary>
    /// Envía un correo electrónico con un único adjunto utilizando el proveedor configurado.
    /// </summary>
    /// <param name="toEmail">
    /// Dirección de correo electrónico del destinatario principal.
    /// Debe ser una dirección válida en formato estándar.
    /// </param>
    /// <param name="subject">
    /// Asunto del mensaje. No debe estar vacío o contener únicamente espacios en blanco.
    /// </param>
    /// <param name="htmlBody">
    /// Contenido del correo en formato HTML.  
    /// Se envía como <see cref="Microsoft.Graph.Models.BodyType.Html"/>.
    /// </param>
    /// <param name="attachment">
    /// Archivo adjunto único que se enviará junto con el correo.  
    /// Contiene nombre de archivo, tipo MIME y contenido binario.
    /// </param>
    /// <returns>
    /// Una tarea asincrónica que representa la operación de envío.
    /// </returns>
    /// <exception cref="System.ApplicationException">
    /// Se lanza cuando el proveedor externo (ej. Microsoft Graph) devuelve un error controlado.
    /// </exception>
    /// <exception cref="System.Exception">
    /// Se lanza si ocurre un error inesperado (problemas de red, timeouts, etc.).
    /// </exception>
    Task SendEmailAsync(
        string toEmail, 
        string subject, 
        string htmlBody, 
        EmailAttachmentModel attachment);
}
