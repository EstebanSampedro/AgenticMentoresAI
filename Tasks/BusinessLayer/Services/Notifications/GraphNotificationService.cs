using Academikus.AgenteInteligenteMentoresTareas.Business.Hubs;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Attachments;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.BatchService;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Chat;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.GraphNotification;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Services;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Users;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Academikus.AgenteInteligenteMentoresTareas.Entity.OutPut;
using Academikus.AgenteInteligenteMentoresTareas.Utility.General;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services;

public class GraphNotificationService : IGraphNotificationService
{
    private static readonly ConcurrentDictionary<string, DateTime> _processedMessages = new();
    
    private readonly IGraphService _graphService;
    private readonly IAiBatchService _aiBatchService;
    private readonly IMessageService _messageService;
    private readonly IAttachmentService _atttachmentService;
    private readonly IUserService _userService;
    private readonly IChatService _chatService;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly IHubContext<ChatHub> _hubContext;

    public GraphNotificationService(
        IGraphService graphService,
        IAiBatchService aiBatchService,
        IMessageService messageService,
        IAttachmentService atttachmentService,
        IUserService userService,
        IChatService chatService,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        IHubContext<ChatHub> hubContext)
    {
        _graphService = graphService;
        _aiBatchService = aiBatchService;
        _messageService = messageService;
        _userService = userService;
        _atttachmentService = atttachmentService;
        _chatService = chatService;
        _serviceAccount = serviceAccountOptions.Value;
        _hubContext = hubContext;
    }

    public async Task ProcessNotificationAsync(NotificationValue notification)
    {
        // Lista acumuladora para mensajes nuevos que serán notificados a la UI
        var newMessages = new List<NewMessageModel>();

        try
        {
            // Extraer IDs del recurso notificado por Microsoft Graph
            var chatId = ExtractId(notification.Resource, "chats");
            var messageId = ExtractId(notification.Resource, "messages");

            // Validación temprana para evitar procesamiento innecesario o errores DB
            if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(messageId))
            {
                Console.WriteLine("[ProcessNotification] IDs inválidos en notificación. Se descarta.");
                return;
            }

            // Evita procesar mensajes duplicados (cuando Graph reintenta o NotifyAllMessage)
            if (IsDuplicate(messageId))
                return;

            // Validación de que el chat sea 1:1 (se ignoran las conversaciones grupales)
            if (!await _graphService.IsOneToOneChatAsync(chatId))
                return;

            // Obtiene el detalle del mensaje desde Microsoft Graph
            var message = await _graphService.GetChatMessageAsync(chatId, messageId);
            if (message == null)
                return;

            var messageType = message.MessageType;
            if (messageType != ChatMessageType.Message)
            {
                // Console.WriteLine($"[ProcessNotification] Ignorado mensaje tipo {messageType}");
                return;
            }

            var senderEntraId = message.From?.User?.Id;
            var originalHtmlContent = message.Body?.Content?.Trim() ?? "";
            var hasAttachments = message.Attachments?.Any() ?? false;
            var hasInlineImages = message.HostedContents?.Any(h => h.ContentBytes?.Length > 0) ?? false;

            bool isEmptyMessage =
                string.IsNullOrWhiteSpace(originalHtmlContent) &&
                !hasAttachments &&
                !hasInlineImages;

            if (isEmptyMessage)
            {
                // Console.WriteLine("[ProcessNotification] Ignorado mensaje fantasma (sin texto, sin adjuntos y sin inline images).");
                return;
            }

            if (originalHtmlContent == "&#8203;" || originalHtmlContent == "<div></div>")
            {
                // Continúa solo si hay attachments reales o inline images
                if (!hasAttachments && !hasInlineImages)
                    return;
            }

            ChatIdAssignmentResultDto? assigned = null;

            // Se valida que exista un chat registrado en DB para este usuario
            if (!await _chatService.ChatExistsForUserAsync(chatId, senderEntraId))
            {
                assigned = await _chatService.UpdateChatMsTeamsIdAsync(senderEntraId, chatId);

                if (assigned is null)
                    return;

                await _hubContext
                    .Clients
                    .Group($"mentor:{assigned.MentorEntraId}")
                    .SendAsync("ChatUpserted", new
                    {
                        ChatId = assigned.ChatDbId,
                        MsTeamsChatId = assigned.MsTeamsChatId,
                        StudentId = assigned.StudentDbId,
                        Message = originalHtmlContent
                    });

                Console.WriteLine($"Mensaje nuevo: {assigned.MsTeamsChatId}");
            }

            // Reemplaza URLs de Graph por URLs propias del backend para permitir acceso autenticado
            var parsedUrlsHtmlContent = HtmlUtils.RewriteGraphImageUrls(originalHtmlContent);

            // Limpia HTML y extrae imágenes inline como URLs separadas
            var htmlContent = HtmlUtils.ProcessHtmlAndStripAttachmentsRobust(parsedUrlsHtmlContent);

            // Guarda el mensaje en la conversación activa (DB)
            var messageDbId = await _messageService.SaveMessageAsync(chatId, messageId, senderEntraId, htmlContent, "html");

            // Guarda attachments (archivos/imágenes) y recupera URLs internas generadas
            var urls = await _atttachmentService.SaveAttachmentsAndBuildInternalUrlsAsync(message, messageDbId, chatId);

            // Obtiene el mensaje recién insertado con sus relaciones completas
            var messageEntity = await _messageService
                .GetMessageWithChatAndAttachmentsAsync(chatId, messageId);

            if (messageEntity != null)
            {
                await _chatService.MarkChatAsUnreadIfStudentAsync(messageEntity);

                var newMessageModel = await _messageService.GetMessageResponseAsync(messageEntity);

                if (newMessageModel != null)
                    newMessages.Add(newMessageModel);
            }

            // Recupera info del estudiante (para tracking de batches)
            var student = await _userService.GetStudentByEntraIdAsync(senderEntraId);

            // Se notifica cada mensaje nuevo
            foreach (var newMessage in newMessages)
            {
                var inlineImageAttachments = new List<AttachmentModel>();

                var (cleanText, inlineAttachments) =
                    await _messageService.ProcessInlineImagesAsync(newMessage.Content, urls);

                // Si escribe el estudiante, acumular para IA
                if (newMessage.SenderRole == "Estudiante" && newMessage.IAEnabled)
                {
                    var userStamp = student?.Email ?? _serviceAccount.Email;

                    await _aiBatchService.UpsertAndExtendWindowAsync(
                        newMessage.ChatId,
                        cleanText,
                        inlineAttachments.Select(a => a.DownloadUrl).ToList(),
                        newMessage.MessageId,
                        userStamp
                    );
                }
                else if (newMessage.SenderRole == "Mentor")
                {
                    // Si el mentor interviene, se cancela cualquier batch activo
                    await _aiBatchService.CancelOpenBatchAsync(newMessage.ChatId, "mentor");
                }

                // Obtiene el estado actual de la IA para enviar al frontend
                var updatedAiState = await _chatService.GetAiEnabledStateAsync(chatId);

                // Adjuntos de DB + imágenes inline
                var attachmentsFromDb =
                    await _messageService.MapDbAttachmentsAsync(messageEntity.MessageAttachments);

                var allAttachments = attachmentsFromDb.Concat(inlineImageAttachments).ToList();

                Console.WriteLine($"Estudiante: {newMessage.ChatId}");
                

                var payload = new
                {
                    ChatId = newMessage.ChatId,
                    MessageId = newMessage.MessageId,
                    SenderRole = newMessage.SenderRole,
                    Content = newMessage.Content,
                    ContentType = newMessage.ContentType,
                    Timestamp = newMessage.Timestamp,
                    AiEnabled = updatedAiState,
                    Attachments = allAttachments
                };

                Console.WriteLine($"Payload: {payload}");

                // Broadcast del mensaje al grupo correspondiente en SignalR
                await _hubContext
                    .Clients
                    .Group($"chat:{newMessage.ChatId}")
                    .SendAsync("ReceiveMessage", payload);
            }
        }
        catch (Exception ex)
        {
            // Captura de errores de toda la cadena de ejecución sin detener el servicio
            Console.WriteLine($"[ProcessNotification] Error general: {ex}");
        }
    }

    /// <summary>
    /// Determina si un mensaje ya fue procesado previamente para evitar duplicados.
    /// Microsoft Graph puede reenviar notificaciones o generar múltiples eventos para un mismo mensaje.
    /// De igual manera si existe una suscripción para ambos usuarios en Microsoft Teams se recibirá un duplicado.
    /// </summary>
    /// <param name="messageId">
    /// Identificador único del mensaje de Microsoft Teams (MsteamsMessageId).
    /// </param>
    /// <returns>
    /// Retorna true si el mensaje ya se procesó anteriormente.
    /// Retorna false si es la primera vez que se ve y lo registra para referencia futura.
    /// </returns>
    private static bool IsDuplicate(string messageId)
    {
        // Si ya existe el MessageId en el diccionario, significa que ese mensaje ya fue procesado
        if (!_processedMessages.TryAdd(messageId, DateTime.UtcNow))
            return true;

        // Registrar el mensaje con el timestamp actual
        _processedMessages[messageId] = DateTime.UtcNow;

        // Limpieza: eliminar mensajes cuya marca de tiempo es más antigua que 5 minutos
        // Esto limita el tamaño del diccionario y evita crecimiento infinito
        foreach (var key in _processedMessages.Keys)
        {
            if (_processedMessages[key] < DateTime.UtcNow.AddMinutes(-5))
                _processedMessages.TryRemove(key, out _);
        }

        // Como el mensaje no estaba registrado antes, no es duplicado
        return false;
    }

    /// <summary>
    /// Extrae un identificador de un recurso de Microsoft Graph usando un patrón específico.
    /// Por ejemplo: de "chats('12345')/messages('67890')" se puede extraer el valor "12345" o "67890"
    /// dependiendo del prefijo utilizado.
    /// </summary>
    /// <param name="resource">
    /// Cadena recibida en la notificación de Graph (estructura del recurso afectado).
    /// </param>
    /// <param name="prefix">
    /// Prefijo a buscar dentro de la URL del recurso, como "chats" o "messages".
    /// </param>
    /// <returns>
    /// Retorna el valor del identificador si se encuentra; de lo contrario, 
    /// retorna "No encontrado" para evitar errores de referencia nula.
    /// </returns>
    private static string ExtractId(string resource, string prefix)
    {
        // Se construye un patrón Regex basado en el prefijo enviado,
        // buscando valores encerrados entre comillas simples después del prefijo
        var pattern = $@"{prefix}\('([^']+)'\)";

        // Aplicación del patrón al recurso recibido
        var match = Regex.Match(resource, pattern);

        // Se devuelve el primer valor capturado, o un mensaje por defecto si no se encontró coincidencia
        return match.Success ? match.Groups[1].Value : "No encontrado";
    }
}
