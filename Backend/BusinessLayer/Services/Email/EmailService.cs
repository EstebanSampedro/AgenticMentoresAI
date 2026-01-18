using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using GraphMessage = Microsoft.Graph.Models.Message;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Email;

/// <summary>
/// Servicio encargado de enviar correos electrónicos a través de Microsoft Graph,
/// utilizando autenticación de aplicación (app-only) mediante ClientSecretCredential.
/// </summary>
public class EmailService : IEmailService
{
    private readonly GraphServiceClient _graphClient;
    private readonly GraphOptions _graphOptions;

    /// <summary>
    /// Inicializa el servicio creando un GraphServiceClient autenticado con ClientSecretCredential.
    /// </summary>
    /// <param name="azureAdOptions">Opciones de configuración de Azure AD, incluyendo TenantId, ClientId y ClientSecret.</param>
    /// <param name="graphOptions">Opciones adicionales para el uso de Microsoft Graph, como el remitente configurado.</param>
    public EmailService(
        IOptions<AzureAdOptions> azureAdOptions,
        IOptions<GraphOptions> graphOptions)
    {
        var azureAd = azureAdOptions.Value;
        _graphOptions = graphOptions.Value;

        // Se construye una credencial basada en el flujo client_credentials (aplicación).
        // Esto permite enviar correos "en nombre de" un buzón permitido, 
        // siempre que la app tenga permisos Mail.Send (application).
        var credential = new ClientSecretCredential(
            azureAd.TenantId,
            azureAd.ClientId,
            azureAd.ClientSecret);
        
        // Se crea el cliente de Graph con la credencial configurada.
        _graphClient = new GraphServiceClient(credential);
    }

    /// <summary>
    /// Envía un correo electrónico con un adjunto a través de Microsoft Graph.
    /// </summary>
    /// <param name="toEmail">Correo electrónico del destinatario principal.</param>
    /// <param name="subject">Asunto del mensaje.</param>
    /// <param name="htmlBody">Contenido del cuerpo del correo en formato HTML.</param>
    /// <param name="attachment">Adjunto único que se enviará con el correo.</param>
    /// <returns>Tarea asincrónica que completa el envío del correo.</returns>
    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, EmailAttachmentModel attachment)
    {
        // Construcción del mensaje usando el alias GraphMessage (definido en usings).
        var message = new GraphMessage
        {
            Subject = subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = htmlBody
            },

            // Destinatario principal
            ToRecipients = new List<Recipient>
            {
                new Recipient { EmailAddress = new EmailAddress { Address = toEmail } }
            },

            // CC configurado temporalmente (TO DO: ELIMINAR antes de producción)
            CcRecipients = new List<Recipient>
            {
                new Recipient { EmailAddress = new EmailAddress { Address = "carlosandres.morales@udla.edu.ec" } }
            },
            
            // Se adjunta un único archivo utilizando FileAttachment
            Attachments = new List<Attachment>
            {
                new FileAttachment
                {
                    // Tipo OData requerido por Graph para adjuntos de archivo
                    OdataType = "#microsoft.graph.fileAttachment",

                    // Nombre del archivo adjunto
                    Name = attachment.FileName,

                    // Tipo MIME detectado o enviado por el cliente
                    ContentType = attachment.ContentType,

                    // Contenido binario ya decodificado desde Base64
                    ContentBytes = attachment.ContentBytes
                }
            }
        };

        // Cuerpo tipado para la operación SendMail
        var body = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
        {
            Message = message,

            // Al tener el valor de true el correo se guarda en la bandeja de enviados del remitente
            SaveToSentItems = true
        };

        try
        {
            // Envío del correo a través de Graph
            await _graphClient
            .Users[_graphOptions.SenderEmail]
            .SendMail
            .PostAsync(body);
        }
        catch (ApiException ex)
        {
            // Error explícito proveniente del SDK moderno de Microsoft Graph (Kiota)
            Console.WriteLine(
                $"Graph API error enviando correo: {ex.Message} | StatusCode: {ex.ResponseStatusCode}");

            throw new ApplicationException("Error enviando correo vía Microsoft Graph.", ex);
        }
        catch (Exception ex)
        {
            // Error de red, timeout u otros casos no controlados por Graph API
            Console.WriteLine($"Error inesperado enviando correo: {ex}");
            throw;
        }
    }
}
