using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.MicrosoftGraph;

public class GraphService : IGraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly IMemoryCache _cache;

    public GraphService(GraphServiceClient graphClient, IMemoryCache cache)
    {
        _graphClient = graphClient;
        _cache = cache;
    }    

    /// <summary>
    /// Determina si un chat en Microsoft Teams es de tipo 1:1.
    /// Utiliza caché para reducir llamadas repetidas al Graph API.
    /// </summary>
    /// <param name="chatId">Identificador del chat en Microsoft Teams.</param>
    /// <returns>
    /// True si el chat es 1:1.
    /// False si es un chat grupal, desconocido o si ocurre un error en la consulta.
    /// </returns>
    public async Task<bool> IsOneToOneChatAsync(string chatId)
    {
        // Validación temprana de chatId
        if (string.IsNullOrWhiteSpace(chatId))
            return false;

        // Se define una clave única de caché basada en el chatId
        var cacheKey = $"chatType:{chatId}";

        // Si ya se consultó este chat recientemente, se recupera desde caché
        if (_cache.TryGetValue(cacheKey, out string cachedChatType))
            return string.Equals(cachedChatType, "oneOnOne", StringComparison.OrdinalIgnoreCase);

        try
        {
            // Consulta remota al Graph API
            var chat = await _graphClient.Chats[chatId].GetAsync();

            // Obtiene el tipo de chat: "oneOnOne", "group" u otros
            var chatType = chat.ChatType?.ToString() ?? "unknown";

            // Se cachea por 1 hora para evitar llamadas repetitivas al Graph
            _cache.Set(cacheKey, chatType, TimeSpan.FromHours(1));

            return string.Equals(chatType, "oneOnOne", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al consultar tipo de chat {chatId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Obtiene un mensaje específico de un chat de Microsoft Teams mediante Graph API.
    /// Incluye cuerpo del mensaje, archivos adjuntos, imágenes inline y datos del remitente.
    /// </summary>
    /// <param name="chatId">Identificador del chat en Microsoft Teams.</param>
    /// <param name="messageId">Identificador del mensaje dentro del chat.</param>
    /// <returns>
    /// Objeto <see cref="ChatMessage"/> si se encuentra correctamente.
    /// Null si ocurre un error o si no existe el mensaje.
    /// </returns>
    public async Task<ChatMessage?> GetChatMessageAsync(string chatId, string messageId)
    {
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(messageId))
            return null;

        try
        {
            // Graph permite seleccionar solo las propiedades necesarias, evitando tráfico innecesario en la red
            return await _graphClient.Chats[chatId].Messages[messageId]
                .GetAsync(config => {
                    // Se seleccionan las propiedades necesarias para el caso de uso actual
                    config.QueryParameters.Select = new[]
                    {
                    "id",
                    "body",
                    "attachments",
                    "from",
                    "createdDateTime",
                    "hostedContents"
                    };
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetChatMessageAsync] Error al recuperar mensaje {messageId}: {ex.Message}");
            return null;
        }
    }

    public async Task<(string? driveId, string? itemId, string? name, string? mime)> 
        ResolveDriveItemAsync(string contentUrl)
    {
        if (string.IsNullOrWhiteSpace(contentUrl))
        {
            Console.WriteLine("[GraphService] ResolveDriveItemAsync: contentUrl es null/vacío.");
            return (null, null, null, null);
        }

        // Graph requiere una "shareId" codificada en Base64URL para obtener DriveItem
        var shareId = "u!" + ToBase64Url(contentUrl);

        try
        {
            var item = await _graphClient.Shares[shareId].DriveItem.GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = new[] 
                { 
                    "id", "name", "file", "parentReference" 
                };
            });

            if (item is null) 
                return (null, null, null, null);

            return
            (
                item.ParentReference?.DriveId,
                item.Id,
                item.Name,
                item.File?.MimeType
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphService] Error al resolver archivo. contentUrl='{contentUrl}'. Error={ex.Message}");
            return (null, null, null, null);
        }
    }

    public async Task<List<AadUserConversationMember>> GetMembersFromChatAsync(string chatId)
    {
        var members = new List<AadUserConversationMember>();

        try
        {
            var response = await _graphClient.Chats[chatId].Members.GetAsync();

            if (response?.Value != null)
            {
                foreach (var member in response.Value)
                {
                    if (member is AadUserConversationMember aadMember)
                    {
                        members.Add(aadMember);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener miembros del chat {chatId}: {ex.Message}");
        }

        return members;
    }

    public async Task<string?> GetOtherUserFromChatAsync(string chatId, string senderEntraId)
    {
        try
        {
            var members = await _graphClient.Chats[chatId].Members.GetAsync();

            if (members == null || members.Value == null || members.Value.Count == 0)
                return null;

            // Buscar el userId que NO coincide con el remitente
            var otherUser = members.Value
                .OfType<AadUserConversationMember>()
                .FirstOrDefault(m => m.UserId != senderEntraId);

            return otherUser?.UserId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphService] Error obteniendo miembros del chat: {ex.Message}");
            return null;
        }
    }


    public async Task<byte[]?> GetImageContentFromHostedContentAsync(string chatId, string messageId, string hostedContentId)
    {
        try
        {
            var stream = await _graphClient
                .Chats[chatId]
                .Messages[messageId]
                .HostedContents[hostedContentId]
                .Content
                .GetAsync();

            if (stream != null)
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener imagen hostedContent: {ex.Message}");
        }

        return null;
    }

    private static string ToBase64Url(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("ToBase64Url input is null/empty.", nameof(input));

        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var b64 = Convert.ToBase64String(bytes);

        return b64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
