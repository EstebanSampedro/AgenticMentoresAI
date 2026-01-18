using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Sessions;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.CreateLink;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using Newtonsoft.Json;
using System.Linq;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.MicrosoftGraph;

/// <summary>
/// Servicio responsable de manejar las operaciones relacionadas con Microsoft Graph,
/// incluyendo autenticación, gestión de tokens y llamadas al SDK de Graph.
/// </summary>
public class MicrosoftGraphService : IMicrosoftGraphService
{
    private readonly string[] _graphScopes;
    
    private readonly IMemoryCache _cache;
    private readonly IConfidentialClientApplication _confidentialClient;
    private readonly ILroSessionService _lroSessionService;    
    private readonly IMentorLookupService _mentorLookupService;
    private readonly GraphServiceClient _graphClient;
    private readonly IConfiguration _configuration;
    private readonly long _maxFileSizeBytes;

    // Tiempo de vida del Access Token dentro del cache, configurable desde appsettings.
    private readonly TimeSpan _accessTokenCacheTtl;

    /// <summary>
    /// Inicializa el servicio de Microsoft Graph configurando dependencias,
    /// autenticación y parámetros requeridos para las operaciones del SDK.
    /// </summary>
    public MicrosoftGraphService(
        IMemoryCache cache,
        IConfidentialClientApplication confidentialClient, 
        ILroSessionService lroSessionService,
        IMentorLookupService mentorLookupService,
        StaticAccessTokenProvider tokenProvider,
        IConfiguration configuration)
    {
        _cache = cache;
        _confidentialClient = confidentialClient;
        _lroSessionService = lroSessionService;
        _mentorLookupService = mentorLookupService;

        var authenticationProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
        _graphClient = new GraphServiceClient(authenticationProvider);

        _configuration = configuration;

        _graphScopes = (configuration["Graph:Scopes"] ?? "Chat.ReadWrite offline_access")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Tiempo de vida del Access Token dentro del cache, configurable desde appsettings.
        if (!int.TryParse(configuration["Graph:AccessTokenCacheMinutes"], out var accessTokenCacheMinutes))
            accessTokenCacheMinutes = 5;

        _accessTokenCacheTtl = TimeSpan.FromMinutes(accessTokenCacheMinutes);

        // Tamaño máximo de archivo permitido para uploads
        if (!int.TryParse(configuration["Uploads:MaxFileSizeMB"], out var maxFileSizeMb))
            maxFileSizeMb = 20;

        _maxFileSizeBytes = maxFileSizeMb * 1024L * 1024L;
    }

    /// <summary>
    /// Crea un <see cref="GraphServiceClient"/> para el mentor asociado a un chat,
    /// usando el flujo Long-Running OBO para obtener un Access Token fresco.
    /// </summary>
    /// <param name="chatId">
    /// Identificador del chat en Teams utilizado para resolver qué mentor está asociado.
    /// </param>
    /// <returns>
    /// Instancia lista de <see cref="GraphServiceClient"/> si existe una sesión válida.
    /// Retorna <c>null</c> si no existe sesión activa o si se requiere reautenticación.
    /// </returns>
    private async Task<GraphServiceClient?> CreateGraphClientForMentorAsync(string chatId)
    {
        // Se obtiene el UPN/email del mentor a partir del chatId.
        // Este paso determina qué usuario ejecutará la llamada a Graph.
        var upn = await _mentorLookupService.GetMentorEmailByChatIdAsync(chatId);
        if (string.IsNullOrWhiteSpace(upn)) 
            return null;

        // Se busca una sesión activa de larga duración asociada al UPN.
        // Esta sesión contiene el sessionKey necesario para AcquireTokenInLongRunningProcess.
        var session = await _lroSessionService.GetActiveByEmailAsync(upn);

        if (session is null)
        {
            // No existe sesión activa, por lo que el mentor debe abrir nuevamente el tab
            // para iniciar el bootstrap y generar una nueva sessionKey.
            Console.WriteLine($"[OBO] No hay sessionKey activa para {upn}. Requiere reauth.");
            return null;
        }

        Console.WriteLine($"[OBO-ACQUIRE] sessionKey usado = {session.Value.SessionKeyPlain}");

        try
        {
            // Solicita un Access Token fresco mediante Long-running OBO,
            // utilizando el sessionKey guardado en la base de datos.
            var tokenResult = await ((ILongRunningWebApi)_confidentialClient)
                .AcquireTokenInLongRunningProcess(_graphScopes, session.Value.SessionKeyPlain)
                .ExecuteAsync();

            // Se marca que la sesión fue utilizada (auditoría y limpieza de sesiones inactivas).
            await _lroSessionService.UpdateLastUsedAsync(session.Value.Id, upn);

            // Se crea un GraphServiceClient a partir del Access Token obtenido.
            var graphClient = GraphClient.FromAccessToken(tokenResult.AccessToken);

            return graphClient;
        }
        catch (MsalUiRequiredException ex)
        {
            // Indica que el Refresh Token ya no es válido: expiró, se revocó el consentimiento,
            // hubo un CAE, SIF, o cualquier condición que invalida el RT del flujo OBO.
            Console.WriteLine($"[OBO] UI required para {upn}: {ex.Message}");

            // Se invalidan todas las sesiones asociadas al usuario para forzar un nuevo bootstrap.
            await _lroSessionService.DeactivateAllByUserObjectIdAsync(upn);

            return null;
        }
    }

    /// <summary>
    /// Obtiene un <see cref="GraphServiceClient"/> válido para un remitente (mentor o usuario),
    /// utilizando flujo Long-Running OBO y cacheando temporalmente el Access Token.
    /// </summary>
    /// <param name="senderUpnOrOid">
    /// UPN del usuario (si contiene '@') o su ObjectId.
    /// </param>
    /// <returns>
    /// Una tupla que contiene:
    /// - <c>GraphServiceClient</c>: cliente autenticado con un Access Token válido.
    /// - <c>LroId</c>: identificador de la sesión LRO utilizada.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se lanza cuando no existe una sesión LRO activa o cuando se requiere reautenticación.
    /// </exception>
    private async Task<(GraphServiceClient Graph, int LroId)> GetGraphForSenderAsync(string senderUpnOrOid)
    {
        // Se determina si el identificador corresponde a un UPN (email)
        // o a un ObjectId, y se recupera la sesión LRO en consecuencia.
        (int Id, string SessionKeyPlain)? session =
            senderUpnOrOid.Contains("@")
                ? await _lroSessionService.GetActiveByEmailAsync(senderUpnOrOid)
                : await _lroSessionService.GetActiveByUserAsync(senderUpnOrOid);

        // Si no existe una sesión activa, el usuario debe volver a autenticarse (bootstrap OBO).
        if (session is null)
            throw new InvalidOperationException("reauth_required");

        // Clave única para cachear el Access Token por usuario+scopes.
        var atCacheKey = $"graphClient-at:{senderUpnOrOid}:{string.Join(' ', _graphScopes)}";

        // Se intenta recuperar un Access Token previamente guardado en memoria.
        if (_cache.TryGetValue(atCacheKey, out string cachedAccessToken))
        {
            // Se crea el GraphClient a partir del token cacheado.
            var graphFromCache = GraphClient.FromAccessToken(cachedAccessToken);
            return (graphFromCache, session.Value.Id);
        }

        try
        {
            // Se obtiene un Access Token fresco mediante Long-running OBO,
            // utilizando el sessionKey almacenado en la sesión LRO.
            var tokenResult = await ((ILongRunningWebApi)_confidentialClient)
                .AcquireTokenInLongRunningProcess(_graphScopes, session.Value.SessionKeyPlain)
                .ExecuteAsync();

            // Se actualiza la auditoría indicando que la sesión fue utilizada recientemente.
            await _lroSessionService.UpdateLastUsedAsync(session.Value.Id, senderUpnOrOid);

            // Se guarda el Access Token en cache para evitar múltiples llamadas consecutivas a OBO.
            _cache.Set(atCacheKey, tokenResult.AccessToken, TimeSpan.FromMinutes(5));

            // Se crea el cliente Graph autenticado con el nuevo Access Token.
            var graph = GraphClient.FromAccessToken(tokenResult.AccessToken);

            return (graph, session.Value.Id);
        }
        catch (MsalUiRequiredException)
        {
            // El Refresh Token ya no es válido (revocado, expirado o CAE/SIF).
            // Se invalida la sesión LRO para forzar una nueva autenticación.
            await _lroSessionService.DeactivateAllByUserObjectIdAsync(senderUpnOrOid);
            throw new InvalidOperationException("reauth_required");
        }
    }

    // TO DO: Validar si se usa
    public async Task<string?> GetEntraUserIdByEmailAsync(string email)
    {
        try
        {
            var (graph, _) = await GetGraphForSenderAsync(email);

            var user = await graph.Users[email]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName" };
                });

            return user?.Id;
        }
        catch (InvalidOperationException ex) when (ex.Message == "reauth_required")
        {
            // TO DO: Validar
            return null;
        }
        catch (ODataError ex)
        {
            Console.WriteLine($"Graph OData error: {ex.Error?.Code} - {ex.Error?.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener el usuario con email {email}: {ex.Message}");
            return null;
        }
    }

    // TO DO: Validar si se usa
    public async Task<string?> SendMessageToUserAsync(string mentorEntraId, string studentEntraId, string messageContent)
    {
        try
        {
            // Buscar sessionKey activa del MENTOR por OID
            var session = await _lroSessionService.GetActiveByUserAsync(mentorEntraId);
            if (session is null)
            {
                // no hay sesión de larga duración -> el usuario debe abrir el tab para bootstrap
                return null; // o lanza: throw new InvalidOperationException("reauth_required");
            }

            // Access Token fresco desde el token cache (long-running OBO)
            var res = await ((ILongRunningWebApi)_confidentialClient)
                .AcquireTokenInLongRunningProcess(_graphScopes, session.Value.SessionKeyPlain)
                .ExecuteAsync();

            // Graph client v5 con ese AT
            var graphClient = GraphClient.FromAccessToken(res.AccessToken);

            // Crear (o intentar crear) un chat 1:1 entre mentor y estudiante
            var chat = new Microsoft.Graph.Models.Chat
            {
                ChatType = ChatType.OneOnOne,
                Members = new List<ConversationMember>
            {
                new AadUserConversationMember
                {
                    Roles = new List<string> { "owner" },
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "user@odata.bind", $"https://graphClient.microsoft.com/v1.0/users/{mentorEntraId}" }
                    }
                },
                new AadUserConversationMember
                {
                    Roles = new List<string> { "owner" },
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "user@odata.bind", $"https://graphClient.microsoft.com/v1.0/users/{studentEntraId}" }
                    }
                }
            }
            };

            var createdChat = await graphClient.Chats.PostAsync(chat);
            if (createdChat is null || string.IsNullOrWhiteSpace(createdChat.Id))
                return null;

            // Enviar el primer mensaje al chat creado
            var message = new ChatMessage
            {
                Body = new ItemBody
                {
                    Content = messageContent,
                    ContentType = BodyType.Html
                }
            };

            await graphClient.Chats[createdChat.Id].Messages.PostAsync(message);

            // Auditoría de uso de la sesión
            await _lroSessionService.UpdateLastUsedAsync(session.Value.Id, mentorEntraId);

            return createdChat.Id;
        }
        catch (MsalUiRequiredException)
        {
            // El RT se invalidó (consent revocado, CAE/SIF, etc.) → invalidar sesión y pedir bootstrap
            await _lroSessionService.DeactivateAllByUserObjectIdAsync(mentorEntraId);
            return null; // o lanza "reauth_required"
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enviando mensaje proactivo: {ex.Message}");
            return null;
        }
    }

    // TO DO: Validar si se usa
    public async Task<string?> GetOneOnOneChatIdAsync(string mentorId, string studentId)
    {
        try
        {
            // var mentorId = await GetEntraUserIdByEmailAsync(mentorEmail);
            // var studentId = await GetEntraUserIdByEmailAsync(studentEmail);

            //if (string.IsNullOrEmpty(mentorId) || string.IsNullOrEmpty(studentId))
            //{
            //    Console.WriteLine("No se pudo obtener los IDs de los usuarios.");
            //    return null;
            //}

            // Obtener los chats del mentor
            var chats = await _graphClient.Users[mentorId].Chats.GetAsync(config =>
            {
                config.QueryParameters.Filter = "chatType eq 'oneOnOne'";
                config.QueryParameters.Select = new[] { "id", "chatType" };
            });

            if (chats?.Value == null)
                return null;

            foreach (var chat in chats.Value)
            {
                var members = await _graphClient.Chats[chat.Id].Members.GetAsync();

                var userIds = members?.Value?
                    .OfType<AadUserConversationMember>()
                    .Select(m => m.UserId)
                    .ToList();

                if (userIds != null &&
                    userIds.Count == 2 &&
                    userIds.Contains(mentorId) &&
                    userIds.Contains(studentId))
                {
                    return chat.Id;
                }
            }

            Console.WriteLine("No se encontró un chat 1:1 entre los usuarios.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al buscar el chat 1:1: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Envía un mensaje limpio al chat, incluyendo texto y attachments previamente subidos.
    /// El mensaje se envía con un cliente Graph autenticado mediante flujo Long-Running OBO.
    /// </summary>
    /// <param name="chatId">Identificador del chat en Microsoft Teams.</param>
    /// <param name="userContent">Contenido HTML proporcionado por el usuario. Puede ser nulo.</param>
    /// <param name="uploadedFiles">Lista de archivos previamente subidos a OneDrive, con sus URLs y metadatos.</param>
    /// <returns>
    /// Un objeto <see cref="SendMessageResponse"/> indicando éxito o error,
    /// así como el Id del mensaje enviado.
    /// </returns>
    public async Task<SendMessageResponse> SendCleanMessageWithAttachmentsAsync(
        string chatId,
        string? userContent,
        List<UploadFileResponse> uploadedFiles)
    {
        try
        {
            // Crear GraphClient usando la sesión LRO asociada al mentor.
            // Si el mentor no tiene sesión válida, se requiere bootstrap.
            var graph = await CreateGraphClientForMentorAsync(chatId);
            if (graph == null)
            {
                return new SendMessageResponse
                {
                    Success = false,
                    ErrorMessage = "No se pudo crear GraphClient"
                };
            }

            // El contenido debe contener texto, aunque sea invisible, para que Teams acepte attachments.
            var sbContent = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(userContent))
            {
                sbContent.Append(userContent);
            }
            else
            {
                sbContent.Append("&#8203;"); // Espacio invisible si no hay texto
            }

            var attachmentsList = new List<ChatMessageAttachment>();

            // Adjuntar archivos como referencias para que Teams muestre el preview.
            if (uploadedFiles != null && uploadedFiles.Any())
            {
                Console.WriteLine($"[CLEAN-MSG] Agregando {uploadedFiles.Count} attachment(s)");

                foreach (var file in uploadedFiles)
                {
                    //// Identificar si es imagen para mostrarla Inline (Captura)
                    //if (IsImage(file.FileName))
                    //{
                    //    // A. Lógica para Capturas (Inline)
                    //    // Agregamos el tag HTML justo debajo del texto existente
                    //    sbContent.Append($"<br><img src=\"{file.FileUrl}\" alt=\"{file.FileName}\" style=\"max-width: 100%; vertical-align: bottom; margin: 5px 0;\">");
                    //}
                    //else
                    //{
                    //    // B. Lógica común (Tanto PDFs como Imágenes deben ir en la lista de attachments)
                    //    // Esto es necesario para que Teams valide los permisos de lectura del link.

                    //}

                    attachmentsList.Add(new ChatMessageAttachment
                    {
                        Id = file.ItemId ?? Guid.NewGuid().ToString(),
                        ContentType = "reference",
                        ContentUrl = file.FileUrl,
                        Name = file.FileName
                    });
                }
            }

            // Construcción final del mensaje
            var message = new ChatMessage
            {
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = sbContent.ToString() // Aquí va el HTML combinado (Texto + <img src>)
                },
                Attachments = attachmentsList // Aquí van TODOS los archivos (PDFs e Imágenes)
            };

            // Enviar el mensaje al chat especificado.
            var sentMessage = await graph.Chats[chatId].Messages.PostAsync(message);

            return new SendMessageResponse
            {
                Success = true,
                MessageId = sentMessage?.Id,
                SentAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLEAN-MSG] Error: {ex}");
            return new SendMessageResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private bool IsImage(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var ext = Path.GetExtension(fileName).ToLower();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp";
    }

    /// <summary>
    /// Envía un mensaje a un chat de Microsoft Teams, con texto y opcionalmente attachments.
    /// Usa autenticación Long-Running OBO para actuar en nombre del mentor.
    /// </summary>
    /// <param name="chatId">Identificador del chat de Teams.</param>
    /// <param name="request">Objeto que contiene el texto del mensaje y la lista de adjuntos.</param>
    /// <returns>
    /// Un objeto <see cref="SendMessageResponse"/> indicando éxito o error, 
    /// incluyendo el Id del mensaje enviado.
    /// </returns>
    public async Task<SendMessageResponse> SendMessageToTeamsAsync(string chatId, SendMessageRequest request)
    {
        try
        {
            // Crear GraphClient autenticado desde la sesión LRO del mentor.
            var graph = await CreateGraphClientForMentorAsync(chatId);

            if (graph == null)
            {
                return new SendMessageResponse
                {
                    Success = false,
                    ErrorMessage = "No se pudo crear GraphClient"
                };
            }

            string messageContent;
            BodyType contentType = BodyType.Html;

            // Si el usuario envió contenido explícito, se usa directamente.
            if (!string.IsNullOrWhiteSpace(request.Content))
            {
                messageContent = request.Content;
            }
            else
            {
                // Cuando solo hay attachments, Teams requiere un texto mínimo.
                messageContent = request.Attachments?.Any() == true
                    ? ""
                    : string.Empty;
                contentType = BodyType.Text;
            }

            // Crear cuerpo del mensaje
            var message = new ChatMessage
            {
                Body = new ItemBody
                {
                    ContentType = contentType,
                    Content = messageContent
                }
            };

            // Adjuntar archivos si existen.
            if (request.Attachments?.Any() == true)
            {
                message.Attachments = new List<ChatMessageAttachment>();

                foreach (var attachment in request.Attachments)
                {
                    // Crear el attachment compatible con Teams.
                    var teamsAttachment = await CreateTeamsAttachment(graph, attachment);

                    if (teamsAttachment != null)
                    {
                        message.Attachments.Add(teamsAttachment);
                        Console.WriteLine($"[TEAMS-MSG] Attachment agregado: {attachment.Name}");
                    }
                }
            }

            // Enviar mensaje al chat correspondiente.
            var sentMessage = await graph.Chats[chatId].Messages.PostAsync(message);

            return new SendMessageResponse
            {
                Success = true,
                MessageId = sentMessage?.Id,
                SentAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }
        catch (ODataError odataEx)
        {
            Console.WriteLine($"[TEAMS-MSG] Error OData: {odataEx.Error?.Code} - {odataEx.Error?.Message}");
            return new SendMessageResponse
            {
                Success = false,
                ErrorMessage = $"Error de Teams: {odataEx.Error?.Message}"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TEAMS-MSG] Error general: {ex}");
            return new SendMessageResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Sube un archivo al OneDrive del mentor asociado al chat indicado,
    /// usando autenticación Long-Running OBO para obtener un GraphClient válido.
    /// Luego genera un enlace accesible para que pueda ser enviado por Teams.
    /// </summary>
    /// <param name="chatId">Identificador del chat utilizado para resolver al mentor asociado.</param>
    /// <param name="file">Archivo recibido desde el frontend (IFormFile).</param>
    /// <returns>
    /// Un <see cref="UploadFileResponse"/> con el estado de la operación,
    /// incluyendo la URL compartible del archivo, nombre, tamaño y metadatos.
    /// </returns>
    public async Task<UploadFileResponse> UploadFileToGraphAsync(string chatId, IFormFile file)
    {
        // Validación básica del archivo.
        if (file == null || file.Length == 0)
        {
            return new UploadFileResponse
            {
                Success = false,
                ErrorMessage = "Archivo vacío o nulo."
            };
        }

        if (file.Length > _maxFileSizeBytes)
        {
            return new UploadFileResponse
            {
                Success = false,
                ErrorMessage = $"El archivo excede el tamaño máximo permitido de {_maxFileSizeBytes / 1024 / 1024} MB."
            };
        }

        try
        {
            // Crear GraphClient usando la sesión LRO del mentor.
            var graph = await CreateGraphClientForMentorAsync(chatId);

            if (graph == null)
            {
                return new UploadFileResponse
                {
                    Success = false,
                    ErrorMessage = "No se pudo crear GraphClient (token del mentor no disponible)."
                };
            }

            // Obtener información del drive del usuario autenticado.
            var driveInfo = await graph.Me.Drive.GetAsync();

            if (driveInfo?.Id == null)
            {
                return new UploadFileResponse
                {
                    Success = false,
                    ErrorMessage = "No se pudo obtener información del drive"
                };
            }

            // Nombre de la carpeta de uploads configurada en appsettings.
            var folderName = _configuration["Uploads:FolderName"];
            if (string.IsNullOrWhiteSpace(folderName))
                folderName = "Uploads"; // fallback seguro

            // Asegurar que la carpeta exista.
            var targetFolder = await EnsureFolderExistsAsync(graph, folderName);
            if (targetFolder == null)
            {
                return new UploadFileResponse
                {
                    Success = false,
                    ErrorMessage = "No se pudo crear/acceder a la carpeta"
                };
            }

            // Subir archivo (determina automáticamente si es simple o grande).
            var uploadedItem = await UploadFileInternalAsync(graph, driveInfo.Id, targetFolder.Id!, file);
            if (uploadedItem?.Id == null)
            {
                return new UploadFileResponse
                {
                    Success = false,
                    ErrorMessage = "Error durante la subida del archivo"
                };
            }

            // Intentar generar un enlace accesible.
            var sharedUrl = await TryCreateBestShareableLinkAsync(graph, driveInfo.Id, uploadedItem.Id);

            return new UploadFileResponse
            {
                Success = true,
                FileUrl = sharedUrl ?? uploadedItem.WebUrl ?? string.Empty,
                FileName = uploadedItem.Name ?? file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                DriveId = driveInfo.Id,
                ItemId = uploadedItem.Id,
                Size = uploadedItem.Size ?? 0
            };
        }
        catch (ODataError odataEx)
        {
            Console.WriteLine($"[UPLOAD ERROR] Graph OData error: {odataEx.Error?.Code} - {odataEx.Error?.Message}");

            var errorMessage = odataEx.Error?.Code switch
            {
                "itemNotFound" => "Carpeta o archivo no encontrado",
                "accessDenied" => "Acceso denegado - verificar permisos de Graph API",
                "invalidRequest" => "Solicitud inválida - verificar formato del archivo",
                "quotaLimitReached" => "Cuota de almacenamiento excedida",
                "nameAlreadyExists" => "Ya existe un archivo con ese nombre",
                _ => $"Error de Graph API: {odataEx.Error?.Code} - {odataEx.Error?.Message}"
            };

            return new UploadFileResponse { Success = false, ErrorMessage = errorMessage };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UPLOAD ERROR] Error inesperado: {ex}");
            return new UploadFileResponse { Success = false, ErrorMessage = $"Error interno: {ex.Message}" };
        }
    }

    /// <summary>
    /// Crea un attachment compatible con Microsoft Teams a partir de la información
    /// de un archivo almacenado en OneDrive. Incluye la URL de referencia y, si es posible,
    /// un thumbnail para vista previa.
    /// </summary>
    /// <param name="graph">Instancia autenticada de <see cref="GraphServiceClient"/>.</param>
    /// <param name="attachment">Objeto que contiene metadatos del archivo subido.</param>
    /// <returns>
    /// Un objeto <see cref="ChatMessageAttachment"/> listo para usarse en Teams.
    /// Retorna <c>null</c> si ocurre un error o si el attachment no es válido.
    /// </returns>
    private async Task<ChatMessageAttachment?> CreateTeamsAttachment(GraphServiceClient graph, AttachmentModel attachment)
    {
        try
        {
            var fileInfo = new
            {
                downloadUrl = attachment.ContentUrl,  // URL directa o SAS al archivo
                uniqueId = attachment.ItemId ?? Guid.NewGuid().ToString(),
                fileType = DetectFileExtension(attachment.Name)
            };

            var teamsAttachment = new ChatMessageAttachment
            {
                Id = fileInfo.uniqueId,
                ContentType = "application/vnd.microsoft.teams.file.download.info",
                ContentUrl = JsonConvert.SerializeObject(fileInfo),
                Name = attachment.Name
            };

            // Intentar obtener una miniatura desde OneDrive
            if (!string.IsNullOrEmpty(attachment.DriveId))
            {
                try
                {
                    var thumbnailUrl = await GetFileThumbnailUrl(graph, attachment.DriveId, attachment.ItemId);

                    if (!string.IsNullOrEmpty(thumbnailUrl))
                    {
                        teamsAttachment.ThumbnailUrl = thumbnailUrl;
                        Console.WriteLine($"[ATTACHMENT] Thumbnail agregado para: {attachment.Name}");
                    }
                }
                catch (Exception thumbnailException)
                {
                    // Continuar sin thumbnail
                    Console.WriteLine($"[ATTACHMENT] No se pudo obtener thumbnail: {thumbnailException.Message}");                    
                }
            }

            return teamsAttachment;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ATTACHMENT] Error creando attachment {attachment.Name}: {ex.Message}");
            return null;
        }
    }

    private string DetectFileExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "bin";

        // Extraer extensión real
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(ext))
            return "bin";

        // Quitar el punto
        ext = ext.TrimStart('.');

        // Validar contra extensiones comunes
        return ext switch
        {
            "jpg" => "jpg",
            "jpeg" => "jpeg",
            "png" => "png",
            "gif" => "gif",
            "pdf" => "pdf",
            "doc" => "doc",
            "docx" => "docx",
            "xls" => "xls",
            "xlsx" => "xlsx",
            "ppt" => "ppt",
            "pptx" => "pptx",
            "txt" => "txt",
            "csv" => "csv",
            "zip" => "zip",
            _ => "bin" // fallback seguro
        };
    }


    /// <summary>
    /// Obtiene la URL de la miniatura (thumbnail) de un archivo almacenado en OneDrive.
    /// Intenta obtener la miniatura en orden de preferencia: Medium → Large → Small.
    /// </summary>
    /// <param name="graph">Instancia autenticada de <see cref="GraphServiceClient"/>.</param>
    /// <param name="driveId">Identificador del Drive donde se encuentra el archivo.</param>
    /// <param name="itemId">Identificador del archivo dentro del Drive.</param>
    /// <returns>
    /// La URL de la miniatura si está disponible; de lo contrario, <c>null</c>.
    /// </returns>
    private async Task<string?> GetFileThumbnailUrl(
        GraphServiceClient graph, 
        string driveId, 
        string itemId)
    {
        try
        {
            // Validación inicial para evitar llamadas innecesarias a Graph
            if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(itemId))
            {
                Console.WriteLine("[THUMBNAIL] driveId o itemId inválido");
                return null;
            }

            // Solicitar thumbnails disponibles del archivo en OneDrive
            var thumbnails = await graph.Drives[driveId].Items[itemId].Thumbnails.GetAsync();

            // Thumbnails vienen en sets (por ejemplo: 0=default)
            var thumbnail = thumbnails?.Value?.FirstOrDefault();
            if (thumbnail != null)
            {
                // Preferir medium, luego large, luego small
                var thumbnailUrl = 
                    thumbnail.Medium?.Url ?? 
                    thumbnail.Large?.Url ?? 
                    thumbnail.Small?.Url;

                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    return thumbnailUrl;
                }
            }

            Console.WriteLine("[THUMBNAIL] No hay thumbnails disponibles para este archivo");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[THUMBNAIL] Error obteniendo thumbnail: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Garantiza que una carpeta exista dentro del OneDrive del usuario autenticado.
    /// Realiza múltiples estrategias: acceso directo mediante ItemWithPath, búsqueda en Children
    /// y creación explícita de la carpeta. Si todas fallan, retorna null.
    /// </summary>
    /// <param name="graph">Instancia autenticada de <see cref="GraphServiceClient"/>.</param>
    /// <param name="folderName">Nombre de la carpeta a asegurar.</param>
    /// <returns>
    /// Un <see cref="DriveItem"/> que representa la carpeta existente o recién creada,
    /// o <c>null</c> si no fue posible obtenerla.
    /// </returns>
    private async Task<DriveItem?> EnsureFolderExistsAsync(GraphServiceClient graph, string folderName)
    {
        // Obtener información del drive del usuario autenticado
        var driveInfo = await graph.Me.Drive.GetAsync();
        if (driveInfo?.Id == null)
        {
            Console.WriteLine("[FOLDER] No se pudo obtener información del drive");
            return null;
        }

        // MÉTODO 1: Intentar obtener la carpeta mediante ruta absoluta
        try
        {
            var folder = await graph.Drives[driveInfo.Id].Root.ItemWithPath(folderName).GetAsync();
            if (folder?.Folder != null)
            {
                return folder;
            }
        }
        catch (ODataError ex) when (ex.Error?.Code == "itemNotFound")
        {
            Console.WriteLine("[FOLDER] Carpeta no existe, se intenta crear...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FOLDER] Método 1 falló: {ex.Message}");
        }

        // MÉTODO 2: Buscar carpeta en Children de la raíz
        try
        {
            var rootItem = await graph.Drives[driveInfo.Id].Root.GetAsync();
            if (rootItem?.Id != null)
            {
                var children = await graph.Drives[driveInfo.Id].Items[rootItem.Id].Children
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"name eq '{folderName}' and folder ne null";
                    });

                var existingFolder = children?.Value?.FirstOrDefault();
                if (existingFolder != null)
                {
                    return existingFolder;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FOLDER] Método 2 falló: {ex.Message}");
        }

        // MÉTODO 3: Crear carpeta
        try
        {
            var rootItem = await graph.Drives[driveInfo.Id].Root.GetAsync();

            if (rootItem?.Id != null)
            {
                var newFolder = new DriveItem
                {
                    Name = folderName,
                    Folder = new Folder(),
                    AdditionalData = new Dictionary<string, object>
                    {
                        { "@microsoft.graph.conflictBehavior", "rename" }
                    }
                };

                var createdFolder = await graph.Drives[driveInfo.Id]
                    .Items[rootItem.Id]
                    .Children
                    .PostAsync(newFolder);

                return createdFolder;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FOLDER] Método 3 falló: {ex.Message}");
        }

        // FALLBACK: Usar raíz del drive
        try
        {
            var rootFallback = await graph.Drives[driveInfo.Id].Root.GetAsync();
            return rootFallback;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FOLDER] Fallback raíz del drive falló: {ex.Message}");
        }

        Console.WriteLine("[FOLDER] FALLO: Ningún método funcionó");
        return null;
    }

    /// <summary>
    /// Determina el método correcto de subida a OneDrive (simple o chunked) según 
    /// el tamaño del archivo. Genera un nombre único y seguro antes de delegar 
    /// la subida a los métodos especializados.
    /// </summary>
    /// <param name="graph">Instancia autenticada de <see cref="GraphServiceClient"/>.</param>
    /// <param name="driveId">Identificador del drive donde se subirá el archivo.</param>
    /// <param name="folderId">Identificador de la carpeta destino dentro del drive.</param>
    /// <param name="file">Archivo recibido desde el frontend.</param>
    /// <returns>Un <see cref="DriveItem"/> representando el archivo subido.</returns>
    private async Task<DriveItem> UploadFileInternalAsync(
    GraphServiceClient graph,
    string driveId,
    string folderId,
    IFormFile file)
    {
        // Validación del tamaño máximo del archivo 
        if (file.Length > _maxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Archivo supera el límite de {_maxFileSizeBytes / (1024 * 1024)} MB."
            );
        }

        // Límite para decidir entre upload simple o con sesión (4 MB)
        const long LARGE_FILE_THRESHOLD = 4L * 1024 * 1024;

        // Asegurar que el nombre del archivo sea válido y seguro
        var sanitizedName = SanitizeFileName(file.FileName ?? "archivo");

        // Generar un nombre único para evitar conflictos en OneDrive
        var uniqueName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{sanitizedName}";

        // Seleccionar método de subida según tamaño del archivo
        if (file.Length <= LARGE_FILE_THRESHOLD)
        {
            return await UploadSmallFileAsync(graph, driveId, folderId, uniqueName, file);
        }

        return await UploadLargeFileAsync(graph, driveId, folderId, uniqueName, file);
    }

    /// <summary>
    /// Sube un archivo pequeño (≤ 4MB) a una carpeta específica en OneDrive usando
    /// una operación PUT directa. Si el nombre ya existe, genera un nombre alternativo
    /// y reintenta una única vez.
    /// </summary>
    /// <param name="graph">Instancia autenticada de <see cref="GraphServiceClient"/>.</param>
    /// <param name="driveId">Identificador del drive donde se subirá el archivo.</param>
    /// <param name="folderId">Identificador de la carpeta destino dentro del drive.</param>
    /// <param name="fileName">Nombre final del archivo ya sanitizado.</param>
    /// <param name="file">Archivo recibido desde el frontend.</param>
    /// <returns>
    /// Un <see cref="DriveItem"/> que contiene metadatos del archivo subido.
    /// </returns>
    /// <exception cref="InvalidOperationException">Si la subida falla o no se genera un resultado válido.</exception>
    private async Task<DriveItem> UploadSmallFileAsync(
        GraphServiceClient graph,
        string driveId,
        string folderId,
        string fileName,
        IFormFile file)
    {
        using var stream = file.OpenReadStream();

        try
        {
            // Primer intento: subir con el nombre original
            var uploadedItem = await graph
                .Drives[driveId]
                .Items[folderId]
                .ItemWithPath(fileName)
                .Content
                .PutAsync(stream);

            if (uploadedItem?.Id == null)
                throw new InvalidOperationException("El archivo no se creó correctamente en OneDrive.");

            return uploadedItem;
        }
        catch (ODataError ex)
            when (ex.Error?.Code == "nameAlreadyExists")
        {
            // Si existe, crear nombre alternativo
            var alternativeName = 
                $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";

            // Reposicionar stream al inicio
            stream.Position = 0;

            var uploadedItem = await graph
                .Drives[driveId]
                .Items[folderId]
                .ItemWithPath(alternativeName)
                .Content
                .PutAsync(stream);

            if (uploadedItem?.Id == null)
                throw new InvalidOperationException("No se pudo crear el archivo con nombre alternativo.");

            return uploadedItem;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UPLOAD] Error subiendo archivo pequeño {fileName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Sube un archivo grande a OneDrive utilizando una sesión de carga (upload session)
    /// y el mecanismo de subida en chunks recomendado por Microsoft Graph.
    /// </summary>
    /// <param name="graph">Instancia autenticada de <see cref="GraphServiceClient"/>.</param>
    /// <param name="driveId">Identificador del drive donde se subirá el archivo.</param>
    /// <param name="folderId">Identificador de la carpeta destino dentro del drive.</param>
    /// <param name="fileName">Nombre final del archivo, ya sanitizado y único.</param>
    /// <param name="file">Archivo recibido desde el frontend.</param>
    /// <returns>Un <see cref="DriveItem"/> representando el archivo subido.</returns>
    private async Task<DriveItem> UploadLargeFileAsync(
        GraphServiceClient graph,
        string driveId,
        string folderId,
        string fileName,
        IFormFile file)
    {
        // Abrir stream del archivo (se libera automáticamente gracias al using)
        using var stream = file.OpenReadStream();

        // Crear cuerpo de la solicitud para abrir la sesión
        var uploadSessionRequest = new CreateUploadSessionPostRequestBody
        {
            Item = new DriveItemUploadableProperties
            {
                Name = fileName,
                AdditionalData = new Dictionary<string, object>
            {
                { "@microsoft.graph.conflictBehavior", "rename" }
            }
            }
        };

        // Crear la sesión de upload en la carpeta destino
        var uploadSession = await graph.Drives[driveId].Items[folderId]
            .CreateUploadSession
            .PostAsync(uploadSessionRequest);

        if (uploadSession?.UploadUrl == null)
        {
            throw new InvalidOperationException("No se pudo crear la sesión de upload");
        }

        // Tamaño de chunk recomendado (5 MB mínimo para rendimiento óptimo)
        var chunkSizeBytes = 5 * 1024 * 1024; // 5 MB

        // Inicializar la tarea de subida de archivos grandes
        var uploadTask = new LargeFileUploadTask<DriveItem>(
            uploadSession, stream, chunkSizeBytes);

        // Ejecutar subida en chunks
        var uploadResult = await uploadTask.UploadAsync();

        // Validar éxito
        if (!uploadResult.UploadSucceeded || uploadResult.ItemResponse?.Id == null)
            throw new InvalidOperationException("Error durante la subida del archivo grande");

        // Retornar DriveItem del archivo final
        return uploadResult.ItemResponse!;
    }

    private async Task<string?> TryCreateDownloadLinkAsync(
    GraphServiceClient graph,
    string driveId,
    string itemId)
    {
        try
        {
            var permission = await graph
                .Drives[driveId]
                .Items[itemId]
                .CreateLink
                .PostAsync(new CreateLinkPostRequestBody
                {
                    Type = "view",   // obligatorio
                    Scope = "anonymous" // debe ser anónimo para Teams
                });

            return permission?.Link?.WebUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShareLink] Error creating link: {ex.Message}");
            return null;
        }
    }


    /// <summary>
    /// Intenta generar un enlace compartible para un archivo de OneDrive usando
    /// múltiples estrategias. Primero intenta un enlace accesible mejorado y, si falla,
    /// recurre al enlace estándar. Todos los fallos se aíslan.
    /// </summary>
    /// <param name="graph">Cliente autenticado de Microsoft Graph.</param>
    /// <param name="driveId">Identificador del OneDrive donde se encuentra el archivo.</param>
    /// <param name="itemId">Identificador del archivo sobre el cual se generará el enlace.</param>
    /// <returns>
    /// La URL del enlace compartible si alguna estrategia funciona; de lo contrario, <c>null</c>.
    /// </returns>
    private async Task<string?> TryCreateBestShareableLinkAsync( // TryGenerateShareableLinksAsync
        GraphServiceClient graph,
        string driveId,
        string itemId)
    {
        // Validación temprana para evitar excepciones innecesarias
        if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(itemId))
        {
            Console.WriteLine("[UPLOAD] driveId o itemId inválidos para generar enlace.");
            return null;
        }

        // PRIMER INTENTO: Crear enlace accesible mejorado
        try
        {
            var accessibleUrl = await CreateAccessibleFileLink(graph, driveId, itemId);

            if (!string.IsNullOrEmpty(accessibleUrl))
                return accessibleUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UPLOAD] Error en CreateAccessibleFileLink: {ex.Message}");
        }

        // SEGUNDO INTENTO: Crear enlace shareable estándar
        try
        {
            var standardUrl = await CreateShareableLinkAsync(graph, driveId, itemId);
            return standardUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UPLOAD] Error en CreateShareableLinkAsync: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Intenta generar un enlace compartible para un archivo de OneDrive usando varias estrategias
    /// de permisos: anónimo, organización, usuarios existentes y finalmente la WebUrl del ítem.
    /// Cada intento se aísla para evitar que un error interrumpa toda la operación.
    /// </summary>
    /// <param name="graph">Instancia autenticada del GraphServiceClient.</param>
    /// <param name="driveId">Identificador del OneDrive donde se encuentra el archivo.</param>
    /// <param name="itemId">Identificador del archivo para el cual se generará el enlace.</param>
    /// <returns>
    /// Una URL accesible para Teams si alguna estrategia funciona; de lo contrario, <c>null</c>.
    /// </returns>
    private async Task<string?> CreateShareableLinkAsync(
        GraphServiceClient graph, 
        string driveId, 
        string itemId)
    {
        try
        {
            // Primera estrategia: Enlace con permisos existentes de usuario
            try
            {
                var existingUserLink = new CreateLinkPostRequestBody
                {
                    Type = "view",
                    Scope = "users" // Solo funciona si ya existían permisos previos
                };

                var userPermission = await graph
                    .Drives[driveId]
                    .Items[itemId]
                    .CreateLink
                    .PostAsync(existingUserLink);

                if (!string.IsNullOrEmpty(userPermission?.Link?.WebUrl))
                {
                    return userPermission.Link.WebUrl;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LINK] Error creando enlace de usuario: {ex.Message}");
            }
                    
            // Segunda estrategia: Enlace anónimo
            try
            {
                var anonymousLink = new CreateLinkPostRequestBody
                {
                    Type = "view", // Solo lectura
                    Scope = "anonymous" // Accesible sin autenticación
                };

                var permission = await graph
                    .Drives[driveId]
                    .Items[itemId]
                    .CreateLink
                    .PostAsync(anonymousLink);

                if (!string.IsNullOrEmpty(permission?.Link?.WebUrl))
                {
                    return permission.Link.WebUrl;
                }
            }
            catch (ODataError ex) 
                when (ex.Error?.Code == "accessDenied")
            {
                Console.WriteLine($"[LINK] Enlaces anónimos no permitidos en esta organización");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LINK] Error creando enlace anónimo: {ex.Message}");
            }

            // Tercera estrategia: Enlace para toda la organización
            try
            {
                var orgLink = new CreateLinkPostRequestBody
                {
                    Type = "view",
                    Scope = "organization" // Accesible para usuarios de la organización
                };

                var orgPermission = await graph
                    .Drives[driveId]
                    .Items[itemId]
                    .CreateLink
                    .PostAsync(orgLink);

                if (!string.IsNullOrEmpty(orgPermission?.Link?.WebUrl))
                {
                    return orgPermission.Link.WebUrl;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LINK] Error creando enlace organizacional: {ex.Message}");
            }

            // Fallback: WebUrl directo del archivo (no siempre accesible)
            try
            {
                var item = await graph.Drives[driveId].Items[itemId].GetAsync();

                return item?.WebUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LINK] Error obteniendo URL web directa: {ex.Message}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LINK] Error crítico creando enlace: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Genera un enlace accesible para un archivo en OneDrive utilizando permisos de organización.
    /// Este método intenta primero obtener la información del archivo y sus permisos actuales,
    /// luego crear un enlace tipo "organization". Si falla, retorna la URL web básica del archivo.
    /// </summary>
    /// <param name="graph">Instancia autenticada del cliente de Graph.</param>
    /// <param name="driveId">Identificador del OneDrive donde reside el archivo.</param>
    /// <param name="itemId">Identificador del archivo objetivo.</param>
    /// <returns>
    /// Una URL compartible si la operación es exitosa; de lo contrario, <c>null</c>.
    /// </returns>
    private async Task<string?> CreateAccessibleFileLink(
        GraphServiceClient graph, 
        string driveId, 
        string itemId)
    {
        try
        {
            // Obtener metadata del archivo
            var item = await graph.Drives[driveId].Items[itemId].GetAsync();
            if (item == null)
            {
                Console.WriteLine($"[ACCESS] No se pudo obtener información del archivo");
                return null;
            }

            // Consultar permisos actuales (solo informativo)
            try
            {
                var currentPermissions = await graph.Drives[driveId].Items[itemId].Permissions.GetAsync();
            }
            catch (Exception permEx)
            {
                Console.WriteLine($"[ACCESS] No se pudieron obtener permisos actuales: {permEx.Message}");
            }

            // Crear enlace organizacional (solo lectura)
            var shareableLink = new CreateLinkPostRequestBody
            {
                Type = "view",
                Scope = "organization",
                AdditionalData = new Dictionary<string, object>
                {
                    { "password", null }, // Sin contraseña
                    { "message", "Archivo compartido desde Teams" }
                }
            };

            var permission = await graph.Drives[driveId].Items[itemId]
                .CreateLink
                .PostAsync(shareableLink);

            // Validar que el enlace se generó
            if (!string.IsNullOrEmpty(permission?.Link?.WebUrl))
            {
                return permission.Link.WebUrl;
            }

            // Fallback final: usar WebUrl del archivo

            return item.WebUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ACCESS] Error configurando accesibilidad: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Verifica si un enlace público o compartido es accesible realizando primero
    /// una solicitud HEAD. Si HEAD no está permitido, intenta un GET ligero.
    /// </summary>
    /// <param name="fileUrl">URL del archivo en OneDrive o SharePoint.</param>
    /// <returns>
    /// <c>true</c> si el enlace responde con un código exitoso (2xx); de lo contrario <c>false</c>.
    /// </returns>
    private async Task<bool> VerifyLinkAccessibility(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            Console.WriteLine("[VERIFY] URL nula o vacía, no es accesible.");
            return false;
        }

        try
        {
            // Intentar hacer una request HEAD al enlace para verificar accesibilidad
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TeamsClient");

            // Intento 1: HEAD (rápido y sin descargar contenido)
            var headRequest = new HttpRequestMessage(HttpMethod.Head, fileUrl);
            var headResponse = await httpClient.SendAsync(headRequest);

            if (headResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"[VERIFY] Enlace accesible mediante HEAD (Status: {headResponse.StatusCode})");
                return true;
            }

            // Intento 2: GET lightweight sin descargar datos grandes
            var getRequest = new HttpRequestMessage(HttpMethod.Get, fileUrl);
            getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 1); // evita descargar el archivo completo

            var getResponse = await httpClient.SendAsync(getRequest);

            var accessible = getResponse.IsSuccessStatusCode;

            return accessible;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VERIFY] Error verificando enlace: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Limpia y normaliza un nombre de archivo para que sea seguro en OneDrive/SharePoint.
    /// Se eliminan rutas completas, caracteres inválidos, espacios excesivos, secuencias problemáticas
    /// y se limita la longitud del nombre para asegurar compatibilidad.
    /// </summary>
    /// <param name="fileName">Nombre original del archivo proporcionado por el cliente.</param>
    /// <returns>Nombre sanitizado, seguro y compatible con Microsoft Graph.</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "archivo";

        // Normalizar Unicode para evitar colisiones de caracteres equivalentes
        fileName = fileName.Normalize(NormalizationForm.FormC);

        // Eliminar rutas completas tipo "C:\fakepath\file.png"
        fileName = Path.GetFileName(fileName);

        // Caracteres inválidos estándar
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

        // Eliminar unicode slashes similares (／, ∕, etc.)
        sanitized = sanitized
            .Replace("／", "")
            .Replace("∕", "")
            .Replace("＼", "");

        // Evitar problemas con múltiples puntos consecutivos
        while (sanitized.Contains(".."))
            sanitized = sanitized.Replace("..", ".");

        // Recortar espacios
        sanitized = sanitized.Trim();

        if (string.IsNullOrEmpty(sanitized))
            sanitized = "archivo";

        // Limitar longitud general
        if (sanitized.Length > 100)
            sanitized = sanitized[..100];

        return sanitized;
    }
}
