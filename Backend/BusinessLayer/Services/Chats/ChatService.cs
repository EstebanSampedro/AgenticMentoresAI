using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Time;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.AI;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.DTOs;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Academikus.AgenteInteligenteMentoresWebApi.Utility.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Chats;

public class ChatService : IChatService
{
    private readonly DBContext _context;
    private readonly IaiClientService _aiClientService;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly IConfiguration _configuration;

    private readonly string[] _scopes;

    public ChatService(
        DBContext context,
        IaiClientService aiClientService,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        IConfiguration configuration)
    {
        _context = context;
        _aiClientService = aiClientService;
        _serviceAccount = serviceAccountOptions.Value;
        _configuration = configuration;

        _scopes = (configuration["Graph:Scopes"] ?? "Chat.ReadWrite offline_access")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);        
    }

    /// <summary>
    /// Obtiene un conjunto paginado de mensajes asociados a un chat específico,
    /// permitiendo además realizar una búsqueda opcional por contenido.
    /// Incluye la carga de adjuntos relacionados a los mensajes recuperados.
    /// </summary>
    /// <param name="chatId">
    /// Identificador del chat en Microsoft Teams (MsteamsChatId) sobre el cual se realizará la consulta.
    /// </param>
    /// <param name="page">
    /// Número de la página a recuperar. Debe ser mayor o igual a 1.
    /// </param>
    /// <param name="pageSize">
    /// Cantidad de mensajes por página. Debe ser mayor o igual a 1.
    /// </param>
    /// <param name="query">
    /// Texto opcional para filtrar mensajes cuyo contenido contenga la cadena indicada.
    /// </param>
    /// <returns>
    /// Una tupla que contiene:
    /// 1) <c>Messages</c>: Lista paginada de mensajes con sus adjuntos.
    /// 2) <c>TotalCount</c>: Número total de mensajes que cumplen la condición de búsqueda.
    /// </returns>
    public async Task<(List<ChatMessageModel> Messages, int TotalCount)> GetMessagesByChatIdAsync(
        string chatId,
        int page,
        int pageSize,
        string? query)
    {
        // Normaliza parámetros de paginación para evitar valores inválidos.
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        // Consulta base: obtención de mensajes pertenecientes al chat solicitado.
        // Se utiliza navegación por relaciones EF para evitar joins manuales.
        // También se aplica búsqueda por contenido solo si se envía un texto válido.
        var messagesQuery =
            from message in _context.Messages
            where message.Conversation.Chat.MsteamsChatId == chatId
                  && (string.IsNullOrEmpty(query) || EF.Functions.Like(message.MessageContent, $"%{query}%"))
            orderby (message.UpdatedAt ?? message.CreatedAt) descending
            select new
            {
                message.Id,
                message.SenderRole,
                message.MessageContent,
                message.MessageContentType,
                RawDate = (message.UpdatedAt ?? message.CreatedAt)
            };

        // Cantidad total antes de aplicar paginación.
        var totalCount = await messagesQuery.CountAsync();

        // Obtención de la página solicitada.
        var rawMessages = await messagesQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Si la página no contiene mensajes, retornar estructura vacía sin consultar adjuntos.
        if (rawMessages.Count == 0)
            return (new List<ChatMessageModel>(), totalCount);

        // Transformación del resultado anónimo en el modelo final.
        // El formateo de fechas se realiza fuera del LINQ para evitar problemas de traducción en EF.
        var messages = rawMessages.Select(r => new ChatMessageModel
        {
            Id = r.Id,
            SenderRole = r.SenderRole,
            MessageContent = r.MessageContent,
            MessageContentType = r.MessageContentType,
            Date = r.RawDate!.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Attachments = new List<MessageAttachmentItem>()
        }).ToList();

        // Obtiene los IDs de los mensajes de la página para cargar sus adjuntos.
        var messageIds = messages.Select(m => m.Id).ToList();

        // Se consultan solamente los adjuntos asociados a los mensajes paginados.
        // Esto evita cargar adjuntos innecesarios.
        var attachmentsRaw = await _context.MessageAttachments
            .Where(a => messageIds.Contains(a.MessageId) && a.DeletedAt == null)
            .Select(a => new
            {
                a.MessageId,
                a.FileName,
                a.ContentType,
                a.InternalContentUrl,
                a.ContentUrl,
                a.SourceType
            })
            .ToListAsync();

        // Se agrupan los adjuntos por mensaje para asignarlos posteriormente.
        var attachmentsByMessage = attachmentsRaw
            .GroupBy(a => a.MessageId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new MessageAttachmentItem
                {
                    FileName = a.FileName ?? "",

                    FileType = GetFileTypeCategory(a.ContentType, a.FileName),

                    ContentType = string.IsNullOrWhiteSpace(a.ContentType)
                        ? "application/octet-stream"
                        : a.ContentType!,

                    DownloadUrl =
                    a.SourceType == "inline-image"
                        ? (a.InternalContentUrl ?? "")
                        : (a.ContentUrl ?? ""),

                    SourceType = a.SourceType ?? "attachment"
                }).ToList()
            );

        // Asignación final de adjuntos a cada mensaje.
        foreach (var msg in messages)
        {
            // Si el mensaje tiene adjuntos, se asignan; de lo contrario se mantiene la lista vacía creada arriba.
            if (attachmentsByMessage.TryGetValue(msg.Id, out var list))
                msg.Attachments = list;
        }

        return (messages, totalCount);
    }

    /// <summary>
    /// Recupera un conjunto de mensajes que rodean a un mensaje central dentro de un chat.
    /// Permite obtener un número configurable de mensajes anteriores y posteriores al mensaje indicado,
    /// preservando el orden cronológico final.
    /// </summary>
    /// <param name="chatId">
    /// Identificador MSTeams del chat al cual pertenece el mensaje solicitado.
    /// </param>
    /// <param name="messageId">
    /// Identificador interno del mensaje que servirá como punto central de referencia.
    /// </param>
    /// <param name="before">
    /// Cantidad de mensajes anteriores al mensaje central que deben incluirse en el contexto.
    /// Se normaliza a cero si se recibe un valor negativo.
    /// </param>
    /// <param name="after">
    /// Cantidad de mensajes posteriores al mensaje central que deben incluirse en el contexto.
    /// Se normaliza a cero si se recibe un valor negativo.
    /// </param>
    /// <returns>
    /// Una lista de <see cref="ChatMessageModel"/> ordenados cronológicamente,
    /// incluyendo los mensajes previos solicitados, el mensaje central y los posteriores.
    /// Si el mensaje no pertenece al chat indicado, se devuelve una lista vacía.
    /// </returns>
    public async Task<List<ChatMessageModel>> GetMessagesWithContextAsync(string chatId, int messageId, int before, int after)
    {
        // Normalizar parámetros para evitar valores inválidos que perjudiquen la consulta.
        if (before < 0) before = 0;
        if (after < 0) after = 0;

        // Obtener el mensaje central y validar que realmente pertenezca al chat solicitado.
        var centerMessage = await _context.Messages
            .Include(m => m.Conversation)
                .ThenInclude(c => c.Chat)
            .FirstOrDefaultAsync(m =>
                m.Id == messageId &&
                m.Conversation.Chat.MsteamsChatId == chatId);

        // Si no se encuentra un mensaje válido, no existe contexto que recuperar.
        if (centerMessage == null)
            return new List<ChatMessageModel>();

        // Extraer datos clave para evitar repetir navegación en cada consulta.
        var centerDate = centerMessage.CreatedAt;
        var chatInternalId = centerMessage.Conversation.ChatId;

        // Mensajes anteriores en el mismo Chat (sin importar número de conversación)
        var beforeMessages = await _context.Messages
            .Where(m =>
                m.Conversation.ChatId == chatInternalId &&
                m.CreatedAt < centerDate)
            .OrderByDescending(m => m.CreatedAt)
            .Take(before)
            .ToListAsync();

        // Mensajes posteriores en el mismo Chat (sin importar número de conversación)
        var afterMessages = await _context.Messages
            .Where(m =>
                m.Conversation.ChatId == chatInternalId &&
                m.CreatedAt > centerDate)
            .OrderBy(m => m.CreatedAt)
            .Take(after)
            .ToListAsync();

        // Consolidar mensajes en orden cronológico real
        var allMessages = beforeMessages
            .OrderBy(m => m.CreatedAt)
            .Concat(new[] { centerMessage })
            .Concat(afterMessages)
            .ToList();

        // Mapear a ChatMessageModel (aquí aún sin adjuntos)
        var result = allMessages.Select(m => new ChatMessageModel
        {
            Id = m.Id,
            SenderRole = m.SenderRole,
            MessageContent = m.MessageContent,
            MessageContentType = m.MessageContentType,
            Date = ((m.UpdatedAt ?? m.CreatedAt).Value)
                    .ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Attachments = new List<MessageAttachmentItem>()
        }).ToList();

        // Obtener IDs para cargar adjuntos
        var messageIds = result.Select(m => m.Id).ToList();

        var attachmentsRaw = await _context.MessageAttachments
            .Where(a => messageIds.Contains(a.MessageId) && a.DeletedAt == null)
            .Select(a => new
            {
                a.MessageId,
                a.FileName,
                a.ContentType,
                a.InternalContentUrl,
                a.ContentUrl,
                a.SourceType
            })
            .ToListAsync();

        // Agrupar adjuntos por mensaje
        var attachmentsByMessage = attachmentsRaw
            .GroupBy(a => a.MessageId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new MessageAttachmentItem
                {
                    FileName = a.FileName ?? "",
                    FileType = GetFileTypeCategory(a.ContentType, a.FileName),
                    ContentType = string.IsNullOrWhiteSpace(a.ContentType)
                        ? "application/octet-stream"
                        : a.ContentType!,
                    DownloadUrl =
                    a.SourceType == "inline-image"
                        ? (a.InternalContentUrl ?? "")
                        : (a.ContentUrl ?? ""),
                    SourceType = a.SourceType ?? "attachment"
                }).ToList()
            );

        // Asignar adjuntos a cada mensaje
        foreach (var msg in result)
        {
            if (attachmentsByMessage.TryGetValue(msg.Id, out var list))
                msg.Attachments = list;
        }

        return result;
    }

    /// <summary>
    /// Obtiene la lista de chats activos asociados a un mentor específico,
    /// identificado por su correo electrónico.
    /// Incluye la información del mentor y del estudiante relacionada a cada chat.
    /// </summary>
    /// <param name="mentorEmail">
    /// Correo electrónico del mentor para el cual se desean consultar los chats activos.
    /// </param>
    /// <returns>
    /// Una lista de entidades <see cref="Chat"/> que pertenecen al mentor indicado
    /// y que se encuentran actualmente en estado <c>Activo</c>.
    /// </returns>
    public async Task<List<Data.DB.EF.VirtualMentorDB.Entities.Chat>> GetChatsByMentorEmailAsync(string mentorEmail)
    {
        // Normalización del correo para prevenir problemas de comparación case-sensitive.
        mentorEmail = mentorEmail.Trim().ToLowerInvariant();

        // Consulta base:
        // - Se utiliza AsNoTracking porque la información solo se utiliza para lectura.
        // - Se incluyen Mentor y Student para evitar futuras consultas adicionales (N+1).
        // - Se filtran únicamente chats activos del mentor solicitado.
        var query = _context.Chats
            .AsNoTracking()
            .Include(c => c.Mentor)
            .Include(c => c.Student)
            .Where(c =>
                c.ChatState == "Activo" &&
                c.Mentor.Email.ToLower() == mentorEmail);

        return await query.ToListAsync();
    }

    /// <summary>
    /// Recupera un conjunto paginado de resúmenes (<see cref="Summary"/>) pertenecientes a un chat específico,
    /// filtrado por el identificador de Microsoft Teams (<c>MsteamsChatId</c>).  
    /// Además ordena los resultados desde el resumen más reciente al más antiguo.
    /// </summary>
    /// <param name="chatId">
    /// Identificador MSTeams del chat al cual pertenecen los resúmenes.
    /// Este valor debe coincidir con la columna <c>Chats.MsteamsChatId</c>.
    /// </param>
    /// <param name="page">
    /// Número de página a consultar. Se normaliza automáticamente a 1 si se recibe un valor menor a cero.
    /// </param>
    /// <param name="pageSize">
    /// Cantidad de elementos por página. Si se recibe un valor menor a uno, se aplica el valor por defecto de 20.
    /// </param>
    /// <returns>
    /// Una tupla con:
    /// <list type="bullet">
    /// <item><description>
    /// <c>Summaries</c>: Lista paginada de resúmenes mapeados al DTO <see cref="ChatSummaryDto"/>.
    /// </description></item>
    /// <item><description>
    /// <c>TotalCount</c>: Número total de resúmenes asociados al chat antes de aplicar la paginación.
    /// </description></item>
    /// </list>
    /// </returns>
    public async Task<(List<ChatSummaryDto> Summaries, int TotalCount)>
        GetSummariesByChatIdAsync(string chatId, int page, int pageSize)
    {
        // Normaliza los parámetros de paginación para evitar valores inválidos.
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        // Consulta base: se utiliza la navegación EF hacia Chat en lugar de joins explícitos.
        // Esto garantiza que solo se obtengan resúmenes pertenecientes al chat solicitado.
        var query =
            _context.Summaries
            .AsNoTracking()
            .Where(s => s.Chat.MsteamsChatId == chatId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.Summary1,
                s.KeyPoints,
                s.SummaryType,
                s.EscalationReason,
                s.Theme,
                s.Priority,
                s.CreatedAt,
                s.CreatedBy
            });

        // Obtiene el número total de registros antes de aplicar paginación.
        var totalCount = await query.CountAsync();

        // Recupera únicamente la página solicitada.
        var rawSummaries = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Mapeo de la proyección anónima al DTO final.
        // Se formatean los valores, se normalizan campos vacíos y se convierte la fecha a formato UTC estándar.
        var list = rawSummaries.Select(x => new ChatSummaryDto
        {
            Id = x.Id,
            ChatId = chatId,
            Summary = x.Summary1 ?? string.Empty,
            SummaryType = x.SummaryType,
            KeyPoints = string.IsNullOrWhiteSpace(x.KeyPoints) ? null : x.KeyPoints,
            Escalated = x.SummaryType == "IA" || x.SummaryType == "Mentor" ? "true" : "false",
            EscalationReason = string.IsNullOrWhiteSpace(x.EscalationReason) ? null : x.EscalationReason,
            Theme = string.IsNullOrWhiteSpace(x.Theme) ? null : x.Theme,
            Priority = string.IsNullOrWhiteSpace(x.Priority) ? null : x.Priority,
            CreatedAt = x.CreatedAt
                .ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            CreatedBy = x.CreatedBy
        }).ToList();

        return (list, totalCount);
    }

    /// <summary>
    /// Genera un resumen para la conversación activa asociada a un chat determinado.
    /// El método valida que exista el chat, que exista una conversación activa,
    /// que existan mensajes y que el contenido sea útil antes de invocar la IA.
    /// Posteriormente almacena el resumen y retorna el resultado en un <see cref="ChatSummaryDto"/>.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat a procesar.</param>
    /// <param name="type">
    /// Tipo de resumen solicitado. Puede ser "IA", "Mentor" u otro valor permitido por el flujo.
    /// </param>
    /// <returns>
    /// Un <see cref="ServiceResult{ChatSummaryDto}"/> indicando éxito o falla,
    /// junto con un resumen generado o un payload informativo según corresponda.
    /// </returns>
    public async Task<ServiceResult<ChatSummaryDto>> CreateSummaryAsync(string chatId, string type)
    {
        // Se define una marca de tiempo única para toda la ejecución,
        // lo cual asegura consistencia en cualquier dato temporal generado.
        var now = DateTimeOffset.UtcNow;

        // Si no se especifica un tipo de resumen, se usa "IA" por defecto.
        if (string.IsNullOrWhiteSpace(type))
            type = "IA";

        // Busca el chat por su MSTeamsChatId. Se selecciona únicamente el Id interno
        // pues es suficiente para las consultas posteriores.
        var chatRecord = await _context.Chats
            .Where(c => c.MsteamsChatId == chatId)
            .Select(c => new { c.Id })
            .SingleOrDefaultAsync();

        // Si el chat no existe, no se puede continuar con el flujo de resumen.
        if (chatRecord is null)
            return ServiceResult<ChatSummaryDto>.Fail(
                EnumSummaryResult.ChatNotFound,
                $"Chat no encontrado para MSTeamsChatId={chatId}"
            );

        // Obtener la conversación activa asociada al chat.
        // Si no existe, se devuelve un error semántico.
        var activeConversationResult = await GetActiveConversationAsync(chatRecord.Id, chatId, now);
        if (!activeConversationResult.Success)
        {
            // Se retorna el mismo código y mensaje que produjo el método interno,
            // permitiendo un manejo uniforme de fallos.
            return ServiceResult<ChatSummaryDto>.Fail(
                activeConversationResult.Code,
                activeConversationResult.Message
            );
        }

        var activeConversation = activeConversationResult.Data!;

        // Obtener los mensajes de la conversación activa.
        // Si no existen mensajes, la generación de resumen no es posible.
        var messagesResult = await GetConversationMessagesAsync(activeConversation.Id, chatId, type, now);
        if (!messagesResult.Success)
        {
            // Reutilizamos el error del método interno, preservando el contexto.
            return ServiceResult<ChatSummaryDto>.Fail(
                messagesResult.Code,
                messagesResult.Message
            );
        }

        var messages = messagesResult.Data!;

        // Limpieza de mensajes para extraer solo texto útil.
        // Este paso elimina HTML, imágenes y contenido vacío.
        var cleanTextResult = BuildCleanConversationText(messages, chatId, type, now);
        if (!cleanTextResult.Success)
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                cleanTextResult.Code,
                cleanTextResult.Message
            );
        }

        var conversationText = cleanTextResult.Data!;

        // Invocar la IA externa para generar el resumen.
        // Se valida tanto la llamada como el payload devuelto.
        var aiResult = await CallSummaryAiAsync(chatId, type, conversationText, now);
        if (!aiResult.Success)
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                aiResult.Code,
                aiResult.Message
            );
        }

        var aiSummary = aiResult.Data!;

        // Persistir el resumen generado en la base de datos.
        var persistResult = await PersistSummaryAsync(chatId, type, aiSummary, now);
        if (!persistResult.Success)
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                persistResult.Code,
                persistResult.Message
            );
        }

        var summaryId = persistResult.Data!;

        // Construcción del DTO final que será devuelto al controlador.
        var dto = MapSummaryToDto(summaryId, chatId, type, aiSummary, now);

        return ServiceResult<ChatSummaryDto>.Ok(dto);
    }

    /// <summary>
    /// Genera un resumen diario para un chat específico tomando como rango el día anterior,
    /// según la zona horaria configurada en <c>appsettings.json</c>.
    /// Este método actúa como un wrapper que delega la lógica real al overload que recibe
    /// explícitamente la fecha a procesar.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat a procesar.</param>
    /// <param name="type">Tipo de resumen solicitado: “IA”, “Mentor”, etc.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{ChatSummaryDto}"/> indicando éxito o fallo,
    /// junto con el resumen generado o un payload informativo.
    /// </returns>
    public async Task<ServiceResult<ChatSummaryDto>> CreateDailySummaryAsync(string chatId, string type)
    {
        // Validación temprana para evitar ejecuciones innecesarias.
        if (string.IsNullOrWhiteSpace(chatId))
            return ServiceResult<ChatSummaryDto>.Fail(
                EnumSummaryResult.InvalidChatId,
                "chatId requerido."
            );

        // Obtiene la zona horaria definida en configuración (por ejemplo: America/Guayaquil).
        // Si no se encuentra, el helper debe devolver la zona por defecto de Ecuador.
        var tzId = _configuration["Summaries:TimeZone"];
        var tz = TimeZoneHelper.GetEcuadorTimeZone(tzId);

        // Calcula la fecha de "ayer" usando la zona horaria local,
        // evitando depender de UTC y asegurando coherencia en resúmenes diarios.
        var yesterdayLocal = TimeZoneHelper.GetYesterdayLocal(tz);

        // Delegar la ejecución al overload principal
        // para mantener esta firma limpia y reutilizar la lógica centralizada.
        return await CreateDailySummaryAsync(chatId, type, yesterdayLocal);
    }

    /// <summary>
    /// Genera un resumen diario (“Diario”) para un chat en una fecha local específica.
    /// El método valida que exista el chat, que no exista ya un resumen diario para ese día,
    /// obtiene los mensajes del intervalo local convertido a UTC, limpia el contenido,
    /// invoca la IA, persiste el resumen y retorna un <see cref="ChatSummaryDto"/>.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat.</param>
    /// <param name="type">Tipo de resumen. Para resúmenes diarios se usa siempre “Diario”.</param>
    /// <param name="dayLocal">Día local (Ecuador) en formato <see cref="DateOnly"/>.</param>
    /// <returns>Un <see cref="ServiceResult{ChatSummaryDto}"/> con éxito o error contextual.</returns>
    public async Task<ServiceResult<ChatSummaryDto>> CreateDailySummaryAsync(
        string chatId,
        string type,
        DateOnly dayLocal)
    {
        Console.WriteLine(
            $"[DailySummary] Start | chatId={chatId} | type={type} | dayLocal={dayLocal:yyyy-MM-dd}");

        if (string.IsNullOrWhiteSpace(chatId))
            return ServiceResult<ChatSummaryDto>.Fail(
                EnumSummaryResult.InvalidChatId,
                "chatId requerido.");

        // Para un resumen diario, el tipo siempre debe ser “Diario” independientemente del parámetro recibido.
        type = "Diario";

        // Se intenta resolver el Id interno del chat. Si no existe, el flujo no puede continuar.
        var chatResult = await ResolveChatInternalIdAsync(chatId);
        if (!chatResult.Success)
            return ServiceResult<ChatSummaryDto>.Fail(chatResult.Code, chatResult.Message);

        var chatInternalId = chatResult.Data!;

        // Se construye la ventana UTC que representa el día local solicitado.
        var windowResult = BuildUtcWindowForLocalDay(dayLocal);
        if (!windowResult.Success)
            return ServiceResult<ChatSummaryDto>.Fail(windowResult.Code, windowResult.Message);

        var (startUtc, endUtc) = windowResult.Data!;

        // Se valida si ya existe un resumen diario para esta fecha.
        // Si existe, se devuelve inmediatamente el DTO correspondiente.
        var dailyExistingResult = await ValidateExistingDailySummaryAsync(chatInternalId, chatId, dayLocal);
        if (!dailyExistingResult.Success)
            return dailyExistingResult; // ya retorna DTO con explicación

        // Se obtienen todos los mensajes del chat dentro del rango UTC calculado.
        var messagesResult = await GetDailyMessagesAsync(chatInternalId, startUtc, endUtc);
        if (!messagesResult.Success)
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                messagesResult.Code,
                messagesResult.Message
            );
        }

        var messages = messagesResult.Data!;

        // Evitar resumen si todos los mensajes vienen vacíos
        var allEmpty = messages.All(m =>
            string.IsNullOrWhiteSpace(m.MessageContent)
        );

        if (allEmpty)
        {
            Console.WriteLine("[DailySummary] No se genera resumen: todos los mensajes están vacíos.");
            return ServiceResult<ChatSummaryDto>.Fail(
                EnumSummaryResult.NoMessagesInConversation,
                $"No existe contenido para generar un resumen para el {chatId} en este día."
            );
        }

        // Se limpia el contenido HTML de los mensajes y se genera la cadena que será enviada a la IA.
        var cleanedTextResult = BuildCleanDailyConversationText(messages);
        if (!cleanedTextResult.Success)
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                cleanedTextResult.Code,
                cleanedTextResult.Message
            );
        }

        var conversationText = cleanedTextResult.Data!;

        // Se invoca el servicio de IA para generar el resumen diario.
        var aiResult = await CallSummaryAiAsync("", conversationText);
        if (!aiResult.Success)
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                aiResult.Code,
                aiResult.Message
            );
        }

        var aiSummary = aiResult.Data!;

        if (string.IsNullOrWhiteSpace(aiSummary.Overview) ||
            string.IsNullOrWhiteSpace(aiSummary.Theme))
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                EnumSummaryResult.NoMessagesInConversation,
                "No es posible generar un resumen debido a contenido vacío o inválido."
            );
        }

        // Se intenta persistir el resumen generado. Cualquier error se captura y retorna.
        var persistResult = await PersistSummaryAsync(chatId, type, aiSummary);
        if (!persistResult.Success)
        {
            return ServiceResult<ChatSummaryDto>.Fail(
                persistResult.Code,
                persistResult.Message
            );
        }

        var summaryId = persistResult.Data!;        

        // Se construye el DTO final con todos los datos resultantes.
        var dto = MapDailySummaryToDto(summaryId, chatId, type, aiSummary);

        return ServiceResult<ChatSummaryDto>.Ok(dto);
    }

    /// <summary>
    /// Marca un chat como leído estableciendo el indicador <c>IsRead</c> en la base de datos.
    /// </summary>
    /// <param name="chatId">
    /// Identificador del chat en Microsoft Teams (<c>MSTeamsChatId</c>) utilizado para localizar el registro interno.
    /// </param>
    /// <returns>
    /// Retorna <c>true</c> cuando el chat existe y se actualiza correctamente.
    /// Retorna <c>false</c> cuando no se encuentra un chat asociado al identificador proporcionado.
    /// </returns>
    public async Task<bool> MarkChatAsReadAsync(string chatId)
    {
        // Se busca el registro interno del chat utilizando el MSTeamsChatId recibido.
        // FirstOrDefaultAsync permite devolver null cuando no existe coincidencia.
        var chat = await _context.Chats.FirstOrDefaultAsync(c => c.MsteamsChatId == chatId);

        // Si no existe un chat asociado a ese MSTeamsChatId, no hay nada que actualizar.
        if (chat == null)
            return false;

        chat.IsRead = true;
        chat.UpdatedAt = DateTime.UtcNow;
        chat.UpdatedBy = _serviceAccount.Email;

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Actualiza el identificador de Microsoft Teams (<c>MsteamsChatId</c>) para un chat activo 
    /// asociado a un mentor y un estudiante.
    /// </summary>
    /// <param name="mentorId">
    /// Identificador interno del mentor dentro del sistema.
    /// </param>
    /// <param name="studentId">
    /// Identificador interno del estudiante asociado al chat.
    /// </param>
    /// <param name="chatId">
    /// Identificador del chat en Microsoft Teams que se asignará al registro existente.
    /// </param>
    /// <remarks>
    /// Solo se actualiza el chat que se encuentre en estado <c>Activo</c>.  
    /// Si no existe un chat activo que coincida con los parámetros proporcionados, no se realiza ninguna acción.
    /// </remarks>
    public async Task UpdateMsTeamsChatIdAsync(int mentorId, int studentId, string chatId)
    {
        // Se busca el chat activo que coincida con el mentor y el estudiante proporcionados.
        // Solo se considera el chat que mantenga el estado "Activo".
        var chat = await _context.Chats
            .FirstOrDefaultAsync(c => c.MentorId == mentorId && c.StudentId == studentId && c.ChatState == "Activo");

        // Si no se encuentra un chat activo, no hay nada que actualizar.
        if (chat != null)
        {
            chat.MsteamsChatId = chatId;
            chat.UpdatedAt = DateTime.UtcNow;
            chat.UpdatedBy = _serviceAccount.Email;

            _context.Chats.Update(chat);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Actualiza la configuración de inteligencia artificial para un chat específico,
    /// incluyendo el estado de la IA y la razón del cambio. 
    /// También registra el cambio en la tabla <c>ChatIALog</c>
    /// para mantener un historial auditable.
    /// </summary>
    /// <param name="chatId">
    /// Identificador del chat en Microsoft Teams (<c>MsteamsChatId</c>) utilizado para localizar el registro interno.
    /// </param>
    /// <param name="aiState">
    /// Valor que indica si la inteligencia artificial debe quedar activada (<c>true</c>) o desactivada (<c>false</c>).
    /// </param>
    /// <param name="aiChangeReason">
    /// Razón proporcionada para justificar la modificación del estado de la IA.
    /// </param>
    /// <returns>
    /// Retorna <c>true</c> si el chat existe y la actualización fue procesada exitosamente.
    /// Retorna <c>false</c> si no se encuentra un chat asociado al identificador proporcionado.
    /// </returns>
    public async Task<bool> UpdateAISettingsAsync(string chatId, bool aiState, string aiChangeReason)
    {
        // Se busca el chat utilizando el MSTeamsChatId recibido.
        // Se utiliza FirstOrDefaultAsync para permitir que el resultado sea null si no existe coincidencia.
        var chat = await _context.Chats
            .FirstOrDefaultAsync(c => c.MsteamsChatId == chatId);

        // Si no existe un chat asociado al identificador proporcionado, no se puede continuar.
        if (chat == null) 
            return false;

        // Se actualiza el estado de la IA según el valor proporcionado.
        chat.Iaenabled = aiState;
        chat.UpdatedAt = DateTime.UtcNow;
        chat.UpdatedBy = _serviceAccount.Email;

        // Se crea un registro histórico para almacenar la razón del cambio y el nuevo estado de la IA.
        // Este log permite mantener trazabilidad completa de cuándo y por qué cambió la configuración.
        var log = new ChatIalog
        {
            ChatId = chat.Id,
            IachangeReason = aiChangeReason,
            Iastate = aiState,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _serviceAccount.Email,
            UpdatedBy = _serviceAccount.Email
        };

        _context.ChatIalogs.Add(log);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Obtiene el chat activo que vincula a un mentor con un estudiante específico.
    /// </summary>
    /// <param name="mentorId">Identificador interno del mentor.</param>
    /// <param name="studentId">Identificador interno del estudiante.</param>
    /// <returns>
    /// La entidad <see cref="Chat"/> correspondiente al chat activo encontrado,
    /// o null si no existe uno que coincida.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Se produce cuando alguno de los identificadores proporcionados no es válido.
    /// </exception>
    public async Task<Data.DB.EF.VirtualMentorDB.Entities.Chat?> GetChatByMentorAndStudentIdAsync(int mentorId, int studentId)
    {
        // Se validan los identificadores proporcionados antes de ejecutar la consulta
        if (mentorId <= 0)
            throw new ArgumentException("El identificador del mentor debe ser mayor a cero.", nameof(mentorId));

        if (studentId <= 0)
            throw new ArgumentException("El identificador del estudiante debe ser mayor a cero.", nameof(studentId));

        // Se busca el chat activo que vincula al mentor y al estudiante especificados
        return await _context.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.MentorId == mentorId &&
                c.StudentId == studentId &&
                c.ChatState == "Activo");
    }

    /// <summary>
    /// Crea o actualiza el chat correspondiente a un estudiante y un mentor determinado,
    /// desactivando primero todos los chats activos del estudiante.
    /// </summary>
    /// <param name="studentId">Identificador interno del estudiante.</param>
    /// <param name="mentorEmail">Correo del mentor que debe quedar asignado al estudiante.</param>
    /// <remarks>
    /// Si el mentor no existe o no está activo, solamente se desactivan los chats existentes.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Se produce cuando los parámetros proporcionados no son válidos.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error durante el proceso de creación o actualización.
    /// </exception>
    public async Task CreateOrUpdateChatAsync(int studentId, string mentorEmail)
    {
        // Se valida que los parámetros sean correctos para evitar consultas inválidas
        if (studentId <= 0)
            throw new ArgumentException("El identificador del estudiante debe ser mayor a cero.", nameof(studentId));

        if (string.IsNullOrWhiteSpace(mentorEmail))
            throw new ArgumentException("El correo del mentor es obligatorio.", nameof(mentorEmail));

        var normalizedEmail = mentorEmail.Trim().ToLower();
        bool chatToActivate = true;

        try
        {
            // Se busca al mentor activo correspondiente al correo proporcionado
            var mentor = await _context.UserTables
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail &&
                                          u.UserRole == "Mentor" &&
                                          u.UserState == "Activo");

            if (mentor == null)
            {
                Console.WriteLine($"No se encontró un mentor activo con el correo {mentorEmail}.");
                chatToActivate = false;
            }

            // Se desactivan todos los chats activos del estudiante antes de activar el nuevo
            var chatsToDeactivate = await _context.Chats
                .Where(c => c.StudentId == studentId && c.ChatState == "Activo")
                .ToListAsync();

            foreach (var chat in chatsToDeactivate)
            {
                chat.ChatState = "Inactivo";
                chat.UpdatedAt = DateTime.UtcNow;
                chat.UpdatedBy = _serviceAccount.Email;
            }

            if (chatsToDeactivate.Any())
                await _context.SaveChangesAsync();

            // Si el mentor no está activo, no se crea ni activa ningún chat
            if (!chatToActivate)
                return;

            // Se verifica si ya existe un chat entre mentor y estudiante
            var existingChat = await _context.Chats
                .FirstOrDefaultAsync(c => c.MentorId == mentor.Id && c.StudentId == studentId);

            if (existingChat == null)
            {
                // Se crea un nuevo chat y su registro inicial de IA si no existía anteriormente
                var newChat = new Data.DB.EF.VirtualMentorDB.Entities.Chat
                {
                    MentorId = mentor.Id,
                    StudentId = studentId,
                    MsteamsChatId = null,
                    Iaenabled = true,
                    ChatState = "Activo",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = _serviceAccount.Email,
                    UpdatedBy = _serviceAccount.Email
                };

                _context.Chats.Add(newChat);
                await _context.SaveChangesAsync();

                var initialLog = new ChatIalog
                {
                    ChatId = newChat.Id,
                    Iastate = true,
                    IachangeReason = "Configuración inicial",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = _serviceAccount.Email,
                    UpdatedBy = _serviceAccount.Email
                };

                _context.ChatIalogs.Add(initialLog);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Si el chat existe pero está inactivo, se reactiva y se actualizan metadatos
                if (existingChat.ChatState != "Activo")
                {
                    existingChat.ChatState = "Activo";
                    existingChat.UpdatedAt = DateTime.UtcNow;
                    existingChat.UpdatedBy = _serviceAccount.Email;

                    await _context.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en CreateOrUpdateChatAsync para studentId={studentId}, mentorEmail={mentorEmail}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Actualiza un registro de chat en la base de datos.
    /// </summary>
    /// <param name="chat">Entidad de chat que contiene los valores actualizados.</param>
    /// <exception cref="ArgumentNullException">
    /// Se produce cuando la entidad proporcionada es nula.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador del chat no es válido.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task UpdateChatAsync(Data.DB.EF.VirtualMentorDB.Entities.Chat chat)
    {
        // Se valida que la entidad proporcionada sea válida antes de intentar actualizarla
        if (chat == null)
            throw new ArgumentNullException(nameof(chat), "La entidad del chat no puede ser nula.");

        if (chat.Id <= 0)
            throw new ArgumentException("El chat debe tener un identificador válido antes de actualizarse.", nameof(chat));

        try
        {
            // Se adjunta la entidad al contexto si no se encuentra siendo rastreada
            if (_context.Entry(chat).State == EntityState.Detached)
            {
                _context.Chats.Attach(chat);
                _context.Entry(chat).State = EntityState.Modified;
            }

            // Se ejecuta la actualización y se guardan los cambios en base de datos
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Si ocurre un error, se captura, se registra y se relanza la excepción
            Console.WriteLine($"Error actualizando Chat Id={chat.Id}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Desactiva todos los chats asociados a un estudiante, estableciendo su estado en 'Inactivo'.
    /// </summary>
    /// <param name="studentId">Identificador interno del estudiante.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando el identificador proporcionado no es válido.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task DeactivateChatsByStudentIdAsync(int studentId)
    {
        // Se valida que el identificador del estudiante sea válido
        if (studentId <= 0)
            throw new ArgumentException("El identificador del estudiante debe ser mayor a cero.", nameof(studentId));

        try
        {
            // Se obtienen todos los chats asociados al estudiante
            var chats = await _context.Chats
                .Where(c => c.StudentId == studentId)
                .ToListAsync();

            if (!chats.Any())
                return;

            bool updated = false;

            // Se actualiza el estado de los chats solo si aún no están inactivos
            foreach (var chat in chats)
            {
                if (chat.ChatState != "Inactivo")
                {
                    chat.ChatState = "Inactivo";
                    chat.UpdatedAt = DateTime.UtcNow;
                    chat.UpdatedBy = _serviceAccount.Email;
                    updated = true;
                }
            }

            // Se guardan los cambios únicamente cuando se ha modificado al menos un registro
            if (updated)
                await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error desactivando chats para studentId={studentId}. Detalle: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Actualiza el estado de todos los chats asociados a un mentor.
    /// </summary>
    /// <param name="mentorId">Identificador interno del mentor.</param>
    /// <param name="state">Nuevo estado que se aplicará a cada chat.</param>
    /// <exception cref="ArgumentException">
    /// Se produce cuando los parámetros proporcionados no son válidos.
    /// </exception>
    /// <exception cref="Exception">
    /// Se produce cuando ocurre un error al guardar los cambios.
    /// </exception>
    public async Task UpdateChatsByMentorIdAsync(int mentorId, string state)
    {
        // Se valida que los parámetros proporcionados sean correctos
        if (mentorId <= 0)
            throw new ArgumentException("El identificador del mentor debe ser mayor a cero.", nameof(mentorId));

        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("El estado del chat es obligatorio.", nameof(state));

        try
        {
            // Se obtienen los chats asociados al mentor
            var chats = await _context.Chats
                .Where(c => c.MentorId == mentorId)
                .ToListAsync();

            if (!chats.Any())
                return;

            bool updated = false;

            // Se actualiza el estado únicamente si hay un cambio real
            foreach (var chat in chats)
            {
                if (chat.ChatState != state)
                {
                    chat.ChatState = state;
                    chat.UpdatedAt = DateTime.UtcNow;
                    chat.UpdatedBy = _serviceAccount.Email;
                    updated = true;
                }
            }

            // Se guardan los cambios solo cuando existen modificaciones
            if (updated)
                await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error actualizando chats para mentorId={mentorId}. Detalle: {ex}");
            throw;
        }
    }

    // PRIVADOS

    /// <summary>
    /// Obtiene la conversación activa (sin finalizar) asociada a un chat interno
    /// y retorna un <see cref="ServiceResult{T}"/> con la entidad correspondiente.
    /// </summary>
    /// <remarks>
    /// Este método solo busca la última conversación activa y no crea una nueva
    /// en caso de que no exista. Se usa internamente dentro del flujo de creación
    /// de resúmenes.
    /// </remarks>
    private async Task<ServiceResult<Data.DB.EF.VirtualMentorDB.Entities.Conversation>>
        GetActiveConversationAsync(int chatInternalId, string chatId, DateTimeOffset now)
    {
        // Consultar la conversación activa:
        // FinishedAt == null indica que aún no ha sido cerrada.
        // Se busca la última conversación creada (descendente por fecha e Id).
        var activeConversation = await _context.Conversations
            .AsNoTracking()
            .Where(cv => cv.ChatId == chatInternalId && cv.FinishedAt == null)
            .OrderByDescending(cv => cv.CreatedAt)
            .ThenByDescending(cv => cv.Id)
            .FirstOrDefaultAsync();

        // Si no existe conversación activa, retornar error de dominio.
        if (activeConversation is null)
        {
            return ServiceResult<Data.DB.EF.VirtualMentorDB.Entities.Conversation>.Fail(
                EnumSummaryResult.NoActiveConversation,
                "No existe una conversación activa para generar un resumen.",
                null);
        }

        return ServiceResult<Data.DB.EF.VirtualMentorDB.Entities.Conversation>.Ok(activeConversation);
    }

    /// <summary>
    /// Obtiene los mensajes asociados a una conversación específica,
    /// excluyendo aquellos marcados como eliminados, y los retorna ordenados
    /// cronológicamente. Si no existen mensajes válidos, se devuelve un resultado
    /// de error de dominio.
    /// </summary>
    /// <param name="conversationId">
    /// Identificador interno de la conversación cuyas entradas se deben recuperar.
    /// </param>
    /// <param name="chatId">
    /// Identificador MSTeams del chat, utilizado únicamente para construir mensajes
    /// o DTOs de error en niveles superiores.
    /// </param>
    /// <param name="type">
    /// Tipo de resumen solicitado (IA, Mentor, etc.). Aquí no afecta la lógica,
    /// pero se mantiene para coherencia con el flujo general.
    /// </param>
    /// <param name="now">
    /// Marca de tiempo consistente para la operación completa.
    /// </param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> conteniendo:
    /// <list type="bullet">
    /// <item>La lista ordenada de mensajes, si existe contenido válido.</item>
    /// <item>Un error de dominio <c>NoMessagesInConversation</c> si la conversación está vacía.</item>
    /// </list>
    /// </returns>
    private async Task<ServiceResult<List<Data.DB.EF.VirtualMentorDB.Entities.Message>>>
        GetConversationMessagesAsync(int conversationId, string chatId, string type, DateTimeOffset now)
    {
        // Recupera mensajes de la conversación:
        // - AsNoTracking: solo lectura, mejora rendimiento.
        // - DeletedAt == null: se excluyen mensajes marcados como eliminados.
        // - Orden ascendente por fecha y luego por Id para garantizar secuencia estable.
        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.DeletedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync();

        // Si la conversación no tiene mensajes válidos, se retorna un error.
        if (messages.Count == 0)
        {
            return ServiceResult <List<Data.DB.EF.VirtualMentorDB.Entities.Message>>.Fail(
                EnumSummaryResult.NoMessagesInConversation,
                "Conversación activa sin mensajes.");
        }

        return ServiceResult <List<Data.DB.EF.VirtualMentorDB.Entities.Message>>.Ok((messages));
    }

    /// <summary>
    /// Procesa los mensajes de una conversación para generar una versión limpia,
    /// eliminando HTML, imágenes y dejando solo texto visible.
    /// </summary>
    /// <param name="messages">Lista de mensajes válidos de la conversación.</param>
    /// <param name="chatId">Identificador del chat para construir mensajes de error.</param>
    /// <param name="type">Tipo de resumen solicitado (IA, Mentor, etc.).</param>
    /// <param name="now">Marca de tiempo consistente.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> con el texto limpio o un error si no hay contenido útil.
    /// </returns>
    private ServiceResult<string> BuildCleanConversationText(
        List<Data.DB.EF.VirtualMentorDB.Entities.Message> messages,
        string chatId,
        string type,
        DateTimeOffset now)
    {
        // StringBuilder para construir el texto limpio de toda la conversación.
        var builder = new StringBuilder();

        foreach (var message in messages)
        {
            // Procesa el HTML eliminando imágenes y dejando solo texto visible.
            var cleaned = HtmlUtils.ProcessHtmlAndStripImages(message.MessageContent ?? string.Empty);
            var cleanText = cleaned?.CleanText ?? string.Empty;

            // Ignorar mensajes vacíos tras la limpieza.
            if (string.IsNullOrWhiteSpace(cleanText))
                continue;

            // Añadir separador si no es el primer fragmento.
            if (builder.Length > 0)
                builder.Append("; ");

            // Identificar el remitente del mensaje.
            var sender = string.IsNullOrWhiteSpace(message.SenderRole)
                ? "Desconocido"
                : message.SenderRole;

            builder.Append($"{sender}: {cleanText}");
        }

        // Si no quedó contenido útil, se retorna error de dominio.
        if (builder.Length == 0)
        {
            return ServiceResult<string>.Fail(
                EnumSummaryResult.NoMessagesInConversation,
                "La conversación no contiene texto útil para generar un resumen.",
                null);
        }

        return ServiceResult<string>.Ok(builder.ToString());
    }

    /// <summary>
    /// Invoca el servicio externo de IA para generar un resumen basado en el
    /// texto limpio de la conversación.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat.</param>
    /// <param name="type">Tipo de resumen solicitado.</param>
    /// <param name="conversationText">Texto limpio enviado a la IA.</param>
    /// <param name="now">Marca de tiempo consistente.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> con la respuesta de la IA o un error
    /// si no se pudo procesar.
    /// </returns>
    private async Task<ServiceResult<SummaryApiResponse>> CallSummaryAiAsync(
        string chatId,
        string type,
        string conversationText,
        DateTimeOffset now)
    {
        SummaryApiResponse? aiSummary;

        try
        {
            // Llamada al cliente de IA.
            aiSummary = await _aiClientService.CallSummaryAgentAsync(chatId, conversationText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CallSummaryAiAsync error: {ex}");

            return ServiceResult<SummaryApiResponse>.Fail(
                EnumSummaryResult.AiCallFailed,
                "Error al invocar el servicio de IA.",
                null);
        }

        // Validación del payload devuelto por la IA.
        if (aiSummary is null ||
            !aiSummary.Success ||
            aiSummary.Data?.Summary is null)
        {
            return ServiceResult<SummaryApiResponse>.Fail(
                EnumSummaryResult.AiBadPayload,
                "La IA devolvió una respuesta inválida.",
                null);
        }

        // Agregar razón de escalación si corresponde.
        if (type == "Mentor")
            aiSummary.Data.Summary.EscalationReason = "Mentor";

        return ServiceResult<SummaryApiResponse>.Ok(aiSummary);
    }

    /// <summary>
    /// Persiste el resumen generado por la IA en la base de datos.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat.</param>
    /// <param name="type">Tipo de resumen (IA / Mentor).</param>
    /// <param name="aiSummary">Resumen generado por la IA.</param>
    /// <param name="now">Marca de tiempo consistente.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> con el Id del resumen guardado
    /// o un error si la persistencia falla.
    /// </returns>
    private async Task<ServiceResult<int>> PersistSummaryAsync(
        string chatId,
        string type,
        SummaryApiResponse aiSummary,
        DateTimeOffset now)
    {
        int summaryId;

        try
        {
            // Método existente para guardar el resumen.
            summaryId = await SaveSummaryAsync(chatId, type, aiSummary);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PersistSummaryAsync error: {ex}");

            return ServiceResult<int>.Fail(
                EnumSummaryResult.PersistFailed,
                "El resumen fue generado, pero no pudo guardarse.",
                0);
        }

        return ServiceResult<int>.Ok(summaryId);
    }

    /// <summary>
    /// Persiste un resumen generado por la IA en la base de datos.
    /// Este método encapsula la llamada al método <c>SaveSummaryAsync</c> y maneja
    /// cualquier excepción lanzada durante el proceso de guardado.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat al que pertenece el resumen.</param>
    /// <param name="type">Tipo de resumen a guardar (IA, Mentor, Diario, etc.).</param>
    /// <param name="payload">Objeto <see cref="SummaryPayload"/> con el contenido procesado del resumen.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{Int32}"/> que indica si la operación fue exitosa.
    /// En caso de éxito contiene el Id del resumen recién almacenado.
    /// En caso de falla, contiene el código de error y un valor <c>0</c>.
    /// </returns>
    private async Task<ServiceResult<int>> PersistSummaryAsync(
    string chatId,
    string type,
    SummaryPayload payload)
    {
        try
        {
            // Delegar la persistencia al overload limpio que recibe directamente SummaryPayload.
            var id = await SaveSummaryAsync(chatId, type, payload);

            // Retornar resultado exitoso con el Id generado.
            return ServiceResult<int>.Ok(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PersistSummaryAsync error: {ex}");
            return ServiceResult<int>.Fail(
                EnumSummaryResult.PersistFailed,
                "Error al guardar el resumen.",
                0);
        }
    }

    /// <summary>
    /// Guarda un resumen generado por la IA utilizando el objeto completo de respuesta
    /// (<see cref="SummaryApiResponse"/>) y delega la persistencia al overload que recibe
    /// únicamente el <see cref="SummaryPayload"/>.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat para el cual se almacena el resumen.</param>
    /// <param name="summaryType">Tipo de resumen a guardar (por ejemplo “IA”, “Mentor”, “Diario”).</param>
    /// <param name="aiSummary">Respuesta completa del servicio de IA que contiene el payload del resumen.</param>
    /// <returns>
    /// El Id del resumen almacenado cuando la operación es exitosa.
    /// Retorna 0 si el resumen no es válido o no puede procesarse.
    /// </returns>
    private async Task<int> SaveSummaryAsync(
    string chatId,
    string summaryType,
    SummaryApiResponse aiSummary)
    {
        // Validación rápida: si el payload del resumen no existe, no hay nada que guardar.
        if (aiSummary?.Data?.Summary is null)
            return 0;

        // Delegar al overload que recibe directamente el SummaryPayload.
        // Esto mantiene un único punto de persistencia real.
        return await SaveSummaryAsync(chatId, summaryType, aiSummary.Data.Summary);
    }

    /// <summary>
    /// Persiste un resumen generado por la IA en la base de datos a partir de un 
    /// <see cref="SummaryPayload"/> previamente validado.
    /// </summary>
    /// <param name="chatId">
    /// Identificador MSTeams del chat al cual pertenece el resumen.
    /// </param>
    /// <param name="summaryType">
    /// Tipo de resumen que se está guardando (por ejemplo: "IA", "Mentor", "Diario").
    /// </param>
    /// <param name="payload">
    /// Contenido del resumen generado por la IA, incluyendo overview, puntos clave, prioridad,
    /// escalamiento y metadatos adicionales.
    /// </param>
    /// <returns>
    /// El Id del registro creado en la tabla <c>Summary</c>.
    /// Retorna <c>0</c> si ocurre alguna validación fallida o si no se pudo persistir.
    /// </returns>
    private async Task<int> SaveSummaryAsync(
    string chatId,
    string summaryType,
    SummaryPayload payload)
    {
        // Si no existe chatId o el payload es nulo, no se puede guardar.
        if (string.IsNullOrWhiteSpace(chatId) || payload is null)
            return 0;

        // Recuperar el Id interno del chat en la base de datos.
        var chatDbId = await _context.Chats
            .Where(c => c.MsteamsChatId == chatId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        // Si no se encuentra chat, se registra el error y se aborta.
        if (chatDbId == 0)
        {
            Console.WriteLine($"SaveSummaryAsync: No se encontró Chat.Id para MSTeamsChatId={chatId}");
            return (0);
        }

        // Preparar los puntos clave unificados en una sola cadena.
        string? keyPointsText = null;
        if (payload.KeyPoints is { Count: > 0 })
        {
            var norm = payload.KeyPoints
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())              // limpiar espacios
                .Select(p => p.TrimEnd('.'))        // quitar punto final
                .Select(p => p.Replace(";", ","))   // evitar ";" dentro del ítem
                .ToList();

            if (norm.Count > 0)
                keyPointsText = string.Join("; ", norm);
        }

        // Fecha de creación en UTC.
        var createdAt = DateTime.UtcNow;

        var entity = new Summary
        {
            ChatId = chatDbId,
            Summary1 = (payload.Overview ?? string.Empty).Trim(),
            KeyPoints = keyPointsText,
            SummaryType = summaryType,
            EscalationReason = string.IsNullOrWhiteSpace(payload.EscalationReason) ? null : payload.EscalationReason.Trim(),
            Theme = string.IsNullOrWhiteSpace(payload.Theme) ? null : payload.Theme.Trim(),
            Priority = string.IsNullOrWhiteSpace(payload.Priority) ? null : payload.Priority.Trim(),
            CreatedAt = createdAt,
            CreatedBy = _serviceAccount.Email
        };

        _context.Summaries.Add(entity);
        await _context.SaveChangesAsync();

        return entity.Id;
    }

    /// <summary>
    /// Construye el DTO final del resumen a partir del resultado de la IA
    /// y el Id persistido.
    /// </summary>
    /// <param name="summaryId">Identificador del resumen almacenado.</param>
    /// <param name="chatId">Identificador MSTeams del chat.</param>
    /// <param name="type">Tipo de resumen solicitado.</param>
    /// <param name="aiSummary">Resumen generado por la IA.</param>
    /// <param name="now">Marca de tiempo consistente.</param>
    /// <returns>Instancia armada de <see cref="ChatSummaryDto"/>.</returns>
    private ChatSummaryDto MapSummaryToDto(
        int summaryId,
        string chatId,
        string type,
        SummaryApiResponse aiSummary,
        DateTimeOffset now)
    {
        var payload = aiSummary.Data.Summary;

        return new ChatSummaryDto
        {
            Id = summaryId,
            ChatId = chatId,
            Summary = payload.Overview ?? "Resumen no disponible.",
            SummaryType = type,
            KeyPoints = (payload.KeyPoints is { Count: > 0 })
                ? string.Join(" | ", payload.KeyPoints
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim()))
                : null,
            Escalated = payload.Escalated ? "true" : "false",
            EscalationReason = payload.EscalationReason,
            Theme = payload.Theme?.Trim(),
            Priority = payload.Priority?.Trim(),
            CreatedAt = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            CreatedBy = _serviceAccount.Email
        };
    }

    /// <summary>
    /// Obtiene el identificador interno (Id) de un chat utilizando su MSTeamsChatId.
    /// Si no existe un chat con ese identificador externo, retorna un error especializado.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat a resolver.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> que contiene el Id interno del chat cuando existe.
    /// Si el chat no se encuentra, se devuelve un resultado de error con un mensaje contextual.
    /// </returns>
    private async Task<ServiceResult<int>> ResolveChatInternalIdAsync(string chatId)
    {
        // Se recupera el Id de la tabla de Chats.
        var chat = await _context.Chats
            .Where(c => c.MsteamsChatId == chatId)
            .Select(c => new { c.Id })
            .SingleOrDefaultAsync();

        // Si el chat no existe, registrar log y devolver error de negocio.
        if (chat is null)
        {
            Console.WriteLine($"[DailySummary] Chat no encontrado: {chatId}");
            return ServiceResult<int>.Fail(
                EnumSummaryResult.ChatNotFound,
                $"Chat no encontrado para MSTeamsChatId={chatId}");
        }

        // Retornar Id interno para continuar el flujo normal
        return ServiceResult<int>.Ok(chat.Id);
    }

    /// <summary>
    /// Construye la ventana de tiempo en UTC que corresponde a un día local específico,
    /// utilizando la zona horaria configurada en <c>appsettings.json</c>.
    /// Este método convierte una fecha local a su intervalo exacto en UTC (inicio y fin del día),
    /// lo cual es necesario para consultar mensajes dentro de un rango diario independiente
    /// del desfase horario.
    /// </summary>
    /// <param name="dayLocal">
    /// Fecha local (por ejemplo en zona horaria Ecuador) representada como <see cref="DateOnly"/>.
    /// </param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> que contiene la tupla
    /// (<see cref="DateTimeOffset"/> <c>startUtc</c>, <see cref="DateTimeOffset"/> <c>endUtc</c>).
    /// Si la zona horaria configurada no es válida, se devuelve un error contextual.
    /// </returns>
    private ServiceResult<(DateTimeOffset startUtc, DateTimeOffset endUtc)> BuildUtcWindowForLocalDay(DateOnly dayLocal)
    {
        // Obtener el identificador de la zona horaria desde configuración.
        // Se espera algo como "America/Guayaquil".
        var tzId = _configuration["Summaries:TimeZone"];

        // Resolver la zona horaria; si es null significa que no se pudo mapear correctamente.
        var tz = TimeZoneHelper.GetEcuadorTimeZone(tzId);

        // Validar zona horaria; error si no existe o no puede cargarse.
        if (tz is null)
            return ServiceResult<(DateTimeOffset, DateTimeOffset)>.Fail(
                EnumSummaryResult.InvalidTimeZone,
                $"Zona horaria inválida: {tzId}");

        // Convertir el día local a su intervalo exacto en UTC mediante el helper centralizado.
        // Devuelve un DateTimeOffset para preservar precisión y evitar ambigüedad de zona.
        var (startUtc, endUtc) = TimeZoneHelper.GetUtcWindowForLocalDay(dayLocal, tz);

        // Retornar el intervalo listo para consumo por consultas EF o lógica interna.
        return ServiceResult<(DateTimeOffset, DateTimeOffset)>.Ok((startUtc, endUtc));
    }

    /// <summary>
    /// Verifica si ya existe un resumen diario (“Diario”) para el chat y la fecha local especificada.
    /// Si existe uno o más resúmenes dentro del rango del día local, retorna un resultado de error 
    /// acompañado del resumen más reciente. Si no existe ninguno, retorna éxito con <c>null</c> para 
    /// permitir que el flujo continúe.
    /// </summary>
    /// <param name="chatInternalId">Id interno del chat en base de datos.</param>
    /// <param name="chatId">Identificador MSTeams del chat.</param>
    /// <param name="dayLocal">Día local (zona horaria Ecuador) evaluado para verificar duplicados.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{ChatSummaryDto}"/>:
    /// • Éxito y <c>null</c> si no existe un resumen diario para ese día.<br/>
    /// • Error contextual si ya existe uno o varios resúmenes, devolviendo también un <see cref="ChatSummaryDto"/>.
    /// </returns>
    private async Task<ServiceResult<ChatSummaryDto>> ValidateExistingDailySummaryAsync(
        int chatInternalId,
        string chatId,
        DateOnly dayLocal)
    {
        // Construir inicio y fin del día local sin zona (se aplicará en queries según CreatedAt almacenado).
        var startLocal = new DateTime(dayLocal.Year, dayLocal.Month, dayLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);

        // Buscar todos los resúmenes del tipo “Diario” generados dentro de ese día local.
        var existing = await _context.Summaries
            .AsNoTracking()
            .Where(s =>
                s.ChatId == chatInternalId &&
                s.SummaryType == "Diario" &&
                s.CreatedAt >= startLocal &&
                s.CreatedAt < endLocal)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        // Si no existe resumen para ese día, continuar flujo normal
        if (existing.Count == 0)
            return ServiceResult<ChatSummaryDto>.Ok(null); // sigue flujo normal

        // Tomar el más reciente para retornarlo como payload informativo
        var latest = existing.First();

        // Construcción del DTO representando el resumen encontrado
        var dto = new ChatSummaryDto
        {
            Id = latest.Id,
            ChatId = chatId,
            Summary = latest.Summary1 ?? "",
            SummaryType = latest.SummaryType,
            KeyPoints = latest.KeyPoints,
            EscalationReason = string.IsNullOrWhiteSpace(latest.EscalationReason) ? null : latest.EscalationReason,
            Theme = string.IsNullOrWhiteSpace(latest.Theme) ? null : latest.Theme,
            Priority = string.IsNullOrWhiteSpace(latest.Priority) ? null : latest.Priority,
            CreatedAt = latest.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            CreatedBy = string.IsNullOrWhiteSpace(latest.CreatedBy) ? "ai-daily" : latest.CreatedBy
        };

        // Definir código de resultado según si hay uno o varios resúmenes existentes
        var code = (existing.Count > 1)
            ? EnumSummaryResult.MultipleDailySummariesDetected
            : EnumSummaryResult.DailySummaryAlreadyExists;

        // Mensaje contextual según cantidad encontrada
        var msg = (existing.Count > 1)
            ? $"Ya existen {existing.Count} resúmenes diarios para {dayLocal:yyyy-MM-dd}."
            : $"Ya existe un resumen diario para {dayLocal:yyyy-MM-dd}.";

        // Retornar error con payload informativo
        return ServiceResult<ChatSummaryDto>.Fail(code, msg, dto);
    }

    /// <summary>
    /// Obtiene todos los mensajes asociados a un chat dentro de un intervalo de tiempo definido en UTC.
    /// Este método consolida los mensajes provenientes de todas las conversaciones del chat,
    /// filtrando aquellos eliminados y devolviendo únicamente el texto y rol del remitente.
    /// </summary>
    /// <param name="chatInternalId">Id interno del chat al que pertenecen las conversaciones.</param>
    /// <param name="startUtc">Fecha/hora UTC que marca el inicio del intervalo a consultar.</param>
    /// <param name="endUtc">Fecha/hora UTC que marca el fin del intervalo a consultar.</param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> con una lista de tuplas
    /// <c>(SenderRole, MessageContent)</c> ordenadas cronológicamente.
    /// Si no existen mensajes en ese intervalo, retorna un error contextual.
    /// </returns>
    private async Task<ServiceResult<List<(string SenderRole, string MessageContent)>>>
        GetDailyMessagesAsync(int chatInternalId, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        // Consulta que recupera mensajes de todas las conversaciones del chat, filtrando:
        //  - mensajes eliminados,
        //  - mensajes cuyo CreatedAt está dentro del rango UTC solicitado.
        // Se ordenan primero por fecha y luego por Id para garantizar estabilidad.
        var msgs = await (
            from cv in _context.Conversations.AsNoTracking()
            join m in _context.Messages.AsNoTracking() on cv.Id equals m.ConversationId
            where cv.ChatId == chatInternalId &&
                  m.DeletedAt == null &&
                  m.CreatedAt >= startUtc &&
                  m.CreatedAt < endUtc
            orderby m.CreatedAt, m.Id
            select new { m.SenderRole, m.MessageContent }
        ).ToListAsync();

        // Si no se encontraron mensajes dentro del intervalo, devolver error explícito.
        if (msgs.Count == 0)
            return ServiceResult<List<(string, string)>>.Fail(
                EnumSummaryResult.NoMessagesInConversation,
                "No hay mensajes para el día solicitado.");

        // Convertir los resultados anónimos en tuplas fuertemente tipadas,
        // asegurando valores no nulos para evitar problemas posteriores.
        var list = msgs
            .Select(m => (m.SenderRole ?? "Desconocido", m.MessageContent ?? ""))
            .ToList();

        // Devolver la lista ordenada y lista para ser procesada.
        return ServiceResult<List<(string, string)>>.Ok(list);
    }

    /// <summary>
    /// Procesa una lista de mensajes del día, limpiando contenido HTML
    /// (como etiquetas, imágenes o estilos) y construyendo una representación
    /// textual compacta con el formato: <c>“Rol: Texto; Rol: Texto; …”</c>.
    /// Este texto es utilizado posteriormente como entrada para el servicio de IA.
    /// </summary>
    /// <param name="messages">
    /// Lista de tuplas que contienen el rol del remitente y el contenido original del mensaje.
    /// Los mensajes deben estar previamente ordenados cronológicamente.
    /// </param>
    /// <returns>
    /// Un <see cref="ServiceResult{T}"/> que contiene la cadena limpia generada.
    /// En caso de que no haya contenido útil después de la limpieza, retorna un error contextual.
    /// </returns>
    private ServiceResult<string> BuildCleanDailyConversationText(
        List<(string SenderRole, string MessageContent)> messages)
    {
        // Acumulador de la cadena final que se enviará a la IA.
        var textBuilder = new StringBuilder();

        foreach (var message in messages)
        {
            // Procesar HTML, eliminar imágenes y extraer solo texto visible.
            var cleanResult = HtmlUtils.ProcessHtmlAndStripImages(message.MessageContent);
            var cleanText = cleanResult?.CleanText ?? string.Empty;

            // Si el contenido no tiene texto real, se omite.
            if (string.IsNullOrWhiteSpace(cleanText))
                continue;

            // Agregar separador si ya existe contenido previo.
            if (textBuilder.Length > 0)
                textBuilder.Append("; ");

            // Rol del remitente (si es nulo o vacío, usar "Desconocido").
            var sender = string.IsNullOrWhiteSpace(message.SenderRole)
                ? "Desconocido"
                : message.SenderRole;

            // Añadir fragmento en formato estándar.
            textBuilder.Append($"{sender}: {cleanText}");
        }

        // Si no quedó contenido útil después del proceso, retornar error.
        if (textBuilder.Length == 0)
        {
            return ServiceResult<string>.Fail(
                EnumSummaryResult.NoMessagesInConversation,
                "No hay mensajes útiles para resumir.");
        }

        // Retornar texto limpio listo para enviar al modelo de IA.
        return ServiceResult<string>.Ok(textBuilder.ToString());
    }

    /// <summary>
    /// Llama al microservicio de IA para generar un resumen basado en el texto limpio
    /// construido a partir de los mensajes del día.
    /// </summary>
    /// <param name="chatId">
    /// Identificador MSTeams del chat sobre el cual se solicita el resumen.
    /// </param>
    /// <param name="cleanText">
    /// Texto limpio y procesado que representa la conversación a resumir.
    /// </param>
    /// <returns>
    /// Un <see cref="ServiceResult{SummaryPayload}"/> que indica éxito o fracaso y,
    /// en caso exitoso, contiene el payload devuelto por la IA con el resumen.
    /// </returns>
    private async Task<ServiceResult<SummaryPayload>> CallSummaryAiAsync(string chatId, string cleanText)
    {
        try
        {
            // Llamada al servicio externo de IA que procesa el texto y genera un resumen estructurado.
            // Este método retorna un SummaryApiResponse que contiene información de éxito y el resumen generado.
            var aiResponse = await _aiClientService.CallSummaryAgentAsync(chatId, cleanText);

            // Validación de la respuesta:
            // - aiResponse no debe ser null
            // - Success debe ser true
            // - Data.Summary debe existir y contener el payload esperado
            if (aiResponse is null || !aiResponse.Success || aiResponse.Data?.Summary is null)
            {
                return ServiceResult<SummaryPayload>.Fail(
                    EnumSummaryResult.AiBadPayload,
                    "La IA devolvió una respuesta inválida.");
            }

            // Si es válido, retornar únicamente el payload del resumen generado por la IA.
            return ServiceResult<SummaryPayload>.Ok(aiResponse.Data.Summary);
        }
        catch (Exception ex)
        {
            // Log del error por consola. Se mantiene explicitamente para trazabilidad.
            Console.WriteLine($"[DailySummary] Error IA: {ex}");

            // Cualquier excepción se captura y se retorna un resultado fallido.
            return ServiceResult<SummaryPayload>.Fail(
                EnumSummaryResult.AiCallFailed,
                "Error al invocar IA.");
        }
    }

    /// <summary>
    /// Convierte un <see cref="SummaryPayload"/> generado por IA en un <see cref="ChatSummaryDto"/> listo para la API.
    /// </summary>
    private ChatSummaryDto MapDailySummaryToDto(
        int summaryId,
        string chatId,
        string type,
        SummaryPayload summary)
    {
        var now = DateTimeOffset.UtcNow;

        return new ChatSummaryDto
        {
            Id = summaryId,
            ChatId = chatId,
            Summary = summary.Overview ?? "No se pudo generar un resumen.",
            SummaryType = type,
            KeyPoints = (summary.KeyPoints is { Count: > 0 })
                ? string.Join(" | ", summary.KeyPoints
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim()))
                : null,
            Escalated = summary.Escalated ? "true" : "false",
            EscalationReason = string.IsNullOrWhiteSpace(summary.EscalationReason) ? null : summary.EscalationReason,
            Theme = string.IsNullOrWhiteSpace(summary.Theme) ? null : summary.Theme,
            Priority = string.IsNullOrWhiteSpace(summary.Priority) ? null : summary.Priority,
            CreatedAt = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            CreatedBy = _serviceAccount.Email
        };
    }

    /// <summary>
    /// Determina la categoría general de un archivo en función de su <c>contentType</c>
    /// o, en caso de no estar disponible, en función de su extensión.
    /// </summary>
    /// <param name="contentType">
    /// Tipo MIME reportado por el archivo. Si está presente se utiliza como criterio principal
    /// para categorizar el tipo de archivo.
    /// </param>
    /// <param name="fileName">
    /// Nombre del archivo, utilizado como mecanismo secundario para deducir la categoría
    /// mediante su extensión cuando no se dispone del <c>contentType</c>.
    /// </param>
    /// <returns>
    /// Retorna una cadena corta que representa la categoría del archivo, como 
    /// <c>"image"</c>, <c>"pdf"</c>, <c>"audio"</c>, <c>"video"</c>, <c>"doc"</c>, 
    /// <c>"xls"</c>, <c>"ppt"</c>, <c>"zip"</c> o <c>"file"</c>.
    /// </returns>
    private static string GetFileTypeCategory(string? contentType, string? fileName)
    {
        // Si el contentType está disponible, se utiliza como base principal para inferir la categoría.
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            // Se normaliza para evitar problemas por mayúsculas/minúsculas.
            var ct = contentType.ToLowerInvariant();

            // Se evalúan patrones comunes de tipos MIME para clasificar el archivo.
            if (ct.StartsWith("image/")) return "image";
            if (ct == "application/pdf") return "pdf";
            if (ct.StartsWith("audio/")) return "audio";
            if (ct.StartsWith("video/")) return "video";

            // Identificación de documentos de Word mediante posibles variantes MIME.
            if (ct.Contains("word") || ct.Contains("msword") || ct.Contains("officedocument.wordprocessingml")) 
                return "doc";

            // Identificación de documentos de Excel.
            if (ct.Contains("excel") || ct.Contains("spreadsheetml")) 
                return "xls";

            // Identificación de presentaciones PowerPoint.
            if (ct.Contains("powerpoint") || ct.Contains("presentationml")) 
                return "ppt";

            // Identificación de archivos comprimidos.
            if (ct.Contains("zip") || ct.Contains("compressed")) 
                return "zip";
        }

        // Si no hay contentType, se intenta clasificar por la extensión del archivo.
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            // Se obtiene la extensión en formato seguro y normalizado.
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            // Se utiliza un switch expresivo para mapear las extensiones a categorías conocidas.
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tif" or ".tiff" or ".heic" => "image",
                ".pdf" => "pdf",
                ".doc" or ".docx" => "doc",
                ".xls" or ".xlsx" => "xls",
                ".ppt" or ".pptx" => "ppt",
                ".zip" or ".rar" or ".7z" => "zip",
                ".mp3" or ".wav" or ".ogg" => "audio",
                ".mp4" or ".mov" or ".avi" or ".mkv" => "video",
                _ => "file"
            };
        }

        // Si no se puede deducir la categoría, se retorna "file" como tipo genérico.
        return "file";
    }
}
