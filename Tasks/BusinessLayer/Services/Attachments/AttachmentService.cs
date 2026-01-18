using Academikus.AgenteInteligenteMentoresTareas.Business.Common;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Attachments;

public class AttachmentService : IAttachmentService
{
    private readonly DBContext _context;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly IGraphService _graphService;

    public AttachmentService(
        DBContext context,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        IGraphService graphService)
    {
        _context = context;
        _serviceAccount = serviceAccountOptions.Value;
        _graphService = graphService;
    }

    /// <summary>
    /// Guarda los archivos adjuntos provenientes de un mensaje de Microsoft Teams,
    /// generando sus URLs internas accesibles desde el backend del sistema.
    /// </summary>

    public async Task<List<string>> SaveAttachmentsAndBuildInternalUrlsAsync(ChatMessage message, int messageDbId, string chatId)
    {
        // Obtiene la URL base del backend ya configurada según el ambiente actual
        var baseUrl = EnvironmentHelper.GetBackendBaseUrl();
        var savedAttachments = new List<MessageAttachment>();
        var internalAttachmentUrls = new List<string>();

        // Si el mensaje no contiene adjuntos, no se realiza procesamiento adicional
        if (message.Attachments?.Any() == true)
        {
            // Se recorre la lista de adjuntos del mensaje recibido desde Teams
            foreach (var attachment in message.Attachments)
            {
                if (string.IsNullOrWhiteSpace(attachment.ContentUrl))
                {
                    Console.WriteLine($"Attachment without contentUrl. messageId={message.Id} chatId={chatId}");
                    continue;
                }

                // Se resuelve la metadata del archivo en Microsoft Graph (Drive + Item)
                var (driveId, itemId, name, mime) = await _graphService.ResolveDriveItemAsync(attachment.ContentUrl);

                // Se crea la entidad para almacenarla en la base de datos
                var entity = new MessageAttachment
                {
                    MessageId = messageDbId,
                    ContentUrl = attachment.ContentUrl,
                    InternalContentUrl = "",
                    DriveId = driveId,
                    ItemId = itemId,
                    FileName = attachment.Name,
                    MimeType = mime,
                    ContentType = attachment.ContentType ?? "application/octet-stream",
                    SourceType = "attachment",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _serviceAccount.Email,
                    UpdatedBy = _serviceAccount.Email
                };

                _context.MessageAttachments.Add(entity);
                savedAttachments.Add(entity);
            }
        }

        if (!string.IsNullOrWhiteSpace(message.Body?.Content))
        {
            var inlineImages = ExtractInlineImagesFromHtml(message.Body.Content, messageDbId);

            foreach (var inlineImg in inlineImages)
            {
                // Verificamos si ya guardamos esto arriba para evitar duplicados 
                // (aunque es raro que coincidan ID de Drive con HostedContent)
                if (savedAttachments.Any(x => x.ItemId == inlineImg.ItemId))
                    continue;

                _context.MessageAttachments.Add(inlineImg);
                savedAttachments.Add(inlineImg);
            }
        }

        // Se guardan inicialmente para contar con IDs persistidos y continuar procesamiento
        await _context.SaveChangesAsync();

        // Se generan las URLs internas para exponer los adjuntos
        foreach (var ent in savedAttachments)
        {
            if (string.IsNullOrEmpty(ent.InternalContentUrl))
            {
                ent.InternalContentUrl = $"{baseUrl}/api/attachments/{ent.DriveId}/{ent.ItemId}";
            }

            ent.UpdatedAt = DateTime.UtcNow;
            ent.UpdatedBy = _serviceAccount.Email;

            internalAttachmentUrls.Add(ent.InternalContentUrl);
        }

        // Actualiza los registros con URLs internas definitivas
        _context.MessageAttachments.UpdateRange(savedAttachments);

        await _context.SaveChangesAsync(); 
        
        return internalAttachmentUrls;
    }

    private List<MessageAttachment> ExtractInlineImagesFromHtml(string html, int messageDbId)
    {
        var list = new List<MessageAttachment>();
        var baseUrl = EnvironmentHelper.GetBackendBaseUrl();

        // Estructura: .../chats/{chatId}/messages/{messageId}/hostedContents/{hostedId}/$value
        string pattern = @"src=""(?<url>https://graph\.microsoft\.com/v1\.0/chats/(?<chatId>[^/]+)/messages/(?<messageId>[^/]+)/hostedContents/(?<hostedId>[^/]+)/\$value)""";

        var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            // Obtenemos los datos crudos de la URL de Graph
            var originalGraphUrl = match.Groups["url"].Value;
            var chatId = match.Groups["chatId"].Value;
            var messageId = match.Groups["messageId"].Value;
            var hostedId = match.Groups["hostedId"].Value; // Este suele ser el ID largo en Base64

            // Usamos EscapeDataString porque el hostedId suele tener caracteres raros
            var internalUrl = $"{baseUrl}/api/images/" +
                              $"{Uri.EscapeDataString(chatId)}/" +
                              $"{Uri.EscapeDataString(messageId)}/" +
                              $"{Uri.EscapeDataString(hostedId)}";

            var contentType = "image/png"; // Valor por defecto
            if (originalGraphUrl.Contains("png", StringComparison.OrdinalIgnoreCase))
                contentType = "image/png";
            else if (originalGraphUrl.Contains("jpg", StringComparison.OrdinalIgnoreCase) ||
                     originalGraphUrl.Contains("jpeg", StringComparison.OrdinalIgnoreCase))
                contentType = "image/jpeg";
            else if (originalGraphUrl.Contains("gif", StringComparison.OrdinalIgnoreCase))
                contentType = "image/gif";

            var extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                _ => ".bin"
            };

            var fileName = $"inline_{messageDbId}_{hostedId.Substring(0, 6)}{extension}";

            var entity = new MessageAttachment
            {
                MessageId = messageDbId,
                ContentUrl = originalGraphUrl, // Guardamos la URL original de Graph como referencia
                InternalContentUrl = internalUrl, // Guardamos la URL interna que acabamos de fabricar
                DriveId = null,
                ItemId = Uri.UnescapeDataString(hostedId), // Guardamos el ID limpio
                FileName = fileName,
                MimeType = "image/png",
                ContentType = "image/png",
                SourceType = "inline-image",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _serviceAccount.Email,
                UpdatedBy = _serviceAccount.Email
            };

            list.Add(entity);
        }

        return list;
    }

    private List<MessageAttachment> ExtractInlineImagesFromHtmlOld(string html, int messageDbId)
    {
        var list = new List<MessageAttachment>();
        var baseUrl = EnvironmentHelper.GetBackendBaseUrl(); // Necesitamos saber tu dominio base

        // Regex para buscar TUS urls reescritas: 
        // Busca: src=".../api/images/{chatId}/{messageId}/{hostedId}"
        // Nota: Asumimos que la URL en el HTML ya fue reescrita por tu función RewriteGraphImageUrls
        string pattern = @"src=""(?<url>[^""]*/api/images/(?<chatId>[^/]+)/(?<messageId>[^/]+)/(?<hostedId>[^/""]+))""";

        var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var fullUrl = match.Groups["url"].Value;
            // Decodificamos porque en la URL vienen como %3A, etc.
            var hostedId = Uri.UnescapeDataString(match.Groups["hostedId"].Value);

            var entity = new MessageAttachment
            {
                MessageId = messageDbId,
                ContentUrl = fullUrl, // Guardamos la URL interna directamente como ContentUrl origen
                InternalContentUrl = fullUrl, // Ya es interna
                DriveId = null, // Hosted Content NO tiene DriveId
                ItemId = hostedId,
                FileName = match.Name, // Nombre genérico o extraer del alt
                MimeType = "image/png",   // HostedContent suele ser PNG/JPG
                ContentType = "image/png",
                SourceType = "inline-image", // IMPORTANTE: Diferenciar el origen
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _serviceAccount.Email,
                UpdatedBy = _serviceAccount.Email
            };

            list.Add(entity);
        }

        return list;
    }
}
