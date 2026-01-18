using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Chats;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.DTOs;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

/// <summary>
/// Controlador responsable de gestionar chats, mensajes, resúmenes y ajustes de IA.
/// Incluye soporte para paginación, envío de archivos, mensajes limpios y recuperación de contexto.
/// </summary>
[Route("api")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IMicrosoftGraphService _microsoftGraphService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de chats.
    /// </summary>
    /// <param name="chatService">
    /// Servicio responsable de gestionar chats, mensajes, resúmenes, adjuntos
    /// y operaciones relacionadas con conversaciones entre mentores y estudiantes.
    /// </param>
    /// <param name="configuration">
    /// Proveedor de configuración de la aplicación, utilizado para obtener valores
    /// como el endpoint base del backend y parámetros necesarios para la
    /// construcción de URLs o comportamiento del sistema.
    /// </param>
    public ChatController(
        IChatService chatService,
        IMicrosoftGraphService microsoftGraphService,
        IConfiguration configuration)
    {
        _chatService = chatService;
        _microsoftGraphService = microsoftGraphService;
        _configuration = configuration;
    }

    /// <summary>
    /// Recupera los mensajes asociados a un chat, con soporte para:
    /// paginación, búsqueda por texto y generación del enlace a la siguiente página.
    /// </summary>
    /// <param name="chatId">Identificador del chat de Microsoft Teams.</param>
    /// <param name="page">Número de página solicitada (valor mínimo permitido: 1).</param>
    /// <param name="pageSize">Cantidad de elementos por página (valor mínimo permitido: 1).</param>
    /// <param name="query">Texto opcional para filtrar los mensajes.</param>
    /// <returns>
    /// Un <see cref="WebApiResponseDto{T}"/> que contiene una lista paginada de mensajes
    /// junto con metadatos de navegación.
    /// </returns>
    //[Authorize]
    //[ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("chat/{chatId}/messages")]
    public async Task<ActionResult> GetMessages(
        string chatId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? query = null)
    {
        // Validación inicial del id del chat
        if (string.IsNullOrEmpty(chatId))
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El identificador del chat es requerido.",
                ResponseData = null
            });

        // Asegurar que los valores de paginación sean válidos
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Los valores de 'page' y 'pageSize' deben ser mayores a cero.",
                ResponseData = null
            });
        }

        // Recuperación de mensajes y total utilizando el servicio de dominio
        var (messages, totalCount) = 
            await _chatService.GetMessagesByChatIdAsync(chatId, page, pageSize, query);

        // Manejo de caso vacío
        if (messages == null || !messages.Any())
            return NotFound(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontraron mensajes entre el mentor y el estudiante.",
                ResponseData = null
            });

        // Construcción segura de la URL de siguiente página
        string? nextPageUrl = null;

        // Si aún quedan mensajes más allá de esta página, generar siguiente URL
        if (page * pageSize < totalCount)
        {
            var baseUrl = _configuration["Backend:Endpoint"]?.TrimEnd('/') ?? string.Empty;

            // Reconstrucción segura de parámetros evitando "query=null"
            var queryParam = string.IsNullOrWhiteSpace(query)
                ? string.Empty
                : $"&query={Uri.EscapeDataString(query)}";

            nextPageUrl = $"{baseUrl}{Request.Path}?page={page + 1}&pageSize={pageSize}{queryParam}";
        }

        // Construcción del DTO de respuesta
        var data = new MessagesDto
        {
            Messages = messages,
            UrlNextPage = nextPageUrl,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };

        return Ok(new WebApiResponseDto<MessagesDto>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Mensajes obtenidos con éxito.",
            ResponseData = data
        });
    }

    /// <summary>
    /// Recupera mensajes anteriores y posteriores a un mensaje específico dentro de un chat,
    /// permitiendo navegación contextual hacia mensajes previos y siguientes.
    /// </summary>
    /// <param name="chatId">Identificador del chat en Microsoft Teams.</param>
    /// <param name="messageId">Identificador del mensaje central desde el cual se tomará el contexto.</param>
    /// <param name="before">Cantidad de mensajes previos a recuperar.</param>
    /// <param name="after">Cantidad de mensajes posteriores a recuperar.</param>
    /// <returns>
    /// Un objeto que contiene los mensajes, sus límites y URLs para continuar navegando
    /// hacia mensajes previos o posteriores.
    /// </returns>
    //[Authorize]
    //[ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("chat/{chatId}/messages/{messageId}/context")]
    public async Task<IActionResult> GetMessageContext(
    string chatId,
    int messageId,
    [FromQuery] int before = 10,
    [FromQuery] int after = 10)
    {
        // Validación de chatId
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El ID del chat es requerido.",
                ResponseData = null
            });
        }

        try
        {
            // Recuperar mensajes previos y posteriores
            var messages = await _chatService.GetMessagesWithContextAsync(chatId, messageId, before, after);

            // Validar si hubo resultados
            if (messages == null || !messages.Any())
            {
                return NotFound(new WebApiResponseDto<object>
                {
                    ResponseCode = ResponseTypeCodeDto.NoData,
                    ResponseMessage = "No se encontraron mensajes relacionados.",
                    ResponseData = new List<ChatMessageModel>()
                });
            }

            // Detectar IDs límite del conjunto devuelto
            var firstMessageId = messages.First().Id;
            var lastMessageId = messages.Last().Id;

            // Base URL desde appsettings
            var baseUrl = _configuration["Backend:Endpoint"]?.TrimEnd('/');
            var basePath = $"{baseUrl}/api/chat/{chatId}/messages";

            string? urlBeforePage = null;
            string? urlAfterPage = null;

            // Si existen mensajes previos, generar URL basada en el ID más antiguo del set
            if (before > 0)
            {
                urlBeforePage = $"{basePath}/{firstMessageId}/context?before={before}&after=0";
            }

            // Si existen mensajes posteriores, generar URL basada en el ID más reciente del set
            if (after > 0)
            {
                urlAfterPage = $"{basePath}/{lastMessageId}/context?before=0&after={after}";
            }

            var result = new
            {
                Messages = messages,
                UrlBeforePage = urlBeforePage,
                UrlAfterPage = urlAfterPage
            };

            return Ok(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Ok,
                ResponseMessage = "Mensajes obtenidos con éxito.",
                ResponseData = result
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MSG-CONTEXT] Error obteniendo contexto del mensaje {messageId} en chat {chatId}: {ex}");

            return StatusCode(500, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno del servidor.",
                ResponseData = null
            });
        }
    }

    /// <summary>
    /// Recupera los resúmenes generados para un chat específico,
    /// con paginación y metadatos para navegación entre páginas.
    /// </summary>
    /// <param name="chatId">Identificador del chat de Microsoft Teams.</param>
    /// <param name="page">Número de página solicitada (mínimo permitido: 1).</param>
    /// <param name="pageSize">Cantidad de elementos por página (mínimo permitido: 1).</param>
    /// <returns>
    /// Un <see cref="WebApiResponseDto{T}"/> que contiene la lista paginada de resúmenes
    /// junto con el enlace de la siguiente página si existe.
    /// </returns>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("chat/{chatId}/summary")]
    public async Task<ActionResult> GetSummaries(
        string chatId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 5)
    {
        // Validación inicial del MSTeamsChatId
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El MSTeamsChatId es requerido.",
                ResponseData = null
            });
        }

        // Normalizar valores de paginación para evitar negativos o cero
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 5;

        // Obtener los resúmenes y contador total desde el servicio de negocio
        var (summaries, totalCount) = await _chatService.GetSummariesByChatIdAsync(chatId, page, pageSize);

        // Si no hay resúmenes, retornar mensaje amigable
        if (summaries == null || !summaries.Any())
        {
            return NotFound(new WebApiResponseDto<IEnumerable<ChatSummaryDto>>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontraron resúmenes para este chat.",
                ResponseData = Array.Empty<ChatSummaryDto>()
            });
        }

        // Construcción segura de la URL de siguiente página
        string? nextPageUrl = null;

        if (page * pageSize < totalCount)
        {
            var baseUrl = _configuration["Backend:Endpoint"]?.TrimEnd('/') ?? string.Empty;

            nextPageUrl = $"{baseUrl}{Request.Path}?page={page + 1}&pageSize={pageSize}";
        }

        // Construcción del DTO de respuesta
        var responseDto = new SummariesDto
        {
            Summaries = summaries,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            UrlNextPage = nextPageUrl
        };

        return Ok(new WebApiResponseDto<SummariesDto>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Resúmenes obtenidos con éxito.",
            ResponseData = responseDto
        });
    }

    /// <summary>
    /// Genera un resumen de conversación para un chat específico.
    /// Permite manejar resúmenes diarios o resúmenes regulares.
    /// </summary>
    /// <param name="chatId">Identificador MSTeams del chat.</param>
    /// <param name="body">Objeto de petición que contiene el tipo de resumen.</param>
    /// <returns>Objeto estandarizado <see cref="WebApiResponseDto{T}"/> con el resultado.</returns>
    [Authorize]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("chat/{chatId}/summary")]
    public async Task<ActionResult> CreateSummary(string chatId, [FromBody] SummaryRequest? body)
    {
        // Valida que el identificador de chat venga presente en la ruta.
        // Si está vacío, se devuelve un error estándar de la API.
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest(new WebApiResponseDto<ChatSummaryDto>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El MSTeamsChatId es requerido.",
                ResponseData = null
            });
        }

        // Verifica que el cuerpo del POST cumpla validaciones del modelo.
        // En caso de falla, se devuelve un error con un ResponseData nulo
        // para mantener coherencia con las demás API.
        if (!ModelState.IsValid)
        {
            return BadRequest(new WebApiResponseDto<ChatSummaryDto>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Datos inválidos.",
                ResponseData = null
            });
        }

        try
        {
            // Obtiene el tipo de resumen solicitado por el cliente (Diario, Mentor, etc.)
            var summaryType = body?.SummaryType?.Trim() ?? string.Empty;
            Console.WriteLine($"[SUMMARY] Tipo solicitado: {summaryType}");

            // Selecciona la operación correspondiente según el tipo de resumen solicitado.
            // Esto permite tener un flujo especial para resúmenes diarios.
            ServiceResult<ChatSummaryDto> serviceResult =
                summaryType.Equals("Diario", StringComparison.OrdinalIgnoreCase)
                    ? await _chatService.CreateDailySummaryAsync(chatId, summaryType)
                    : await _chatService.CreateSummaryAsync(chatId, summaryType);

            // El servicio devuelve un objeto que contiene un DTO y metadatos de éxito/error.
            ChatSummaryDto? summary = serviceResult.Data;

            // Si el servicio indica éxito y se generó un resumen, se retorna con código 200.
            if (serviceResult.Success && summary is not null)
            {
                return Ok(new WebApiResponseDto<ChatSummaryDto>
                {
                    ResponseCode = ResponseTypeCodeDto.Ok,
                    ResponseMessage = "Resumen creado con éxito.",
                    ResponseData = summary
                });
            }

            // Casos donde el servicio determina que no existe conversación activa
            // o no hay mensajes para generar un resumen. Se devuelve código 200 con ResponseCode de "NoData".
            if (serviceResult.Code is
                EnumSummaryResult.NoActiveConversation or
                EnumSummaryResult.NoMessagesInConversation)
            {
                return Ok(new WebApiResponseDto<ChatSummaryDto>
                {
                    ResponseCode = ResponseTypeCodeDto.NoData,
                    ResponseMessage = serviceResult.Message ?? "No existen datos para generar el resumen.",
                    ResponseData = summary
                });
            }

            var httpStatus = MapHttpStatus(serviceResult.Code);

            // Para errores reales (IA, persistencia, chat inválido, etc.) se asigna el status HTTP correspondiente.
            return StatusCode(httpStatus, new WebApiResponseDto<ChatSummaryDto>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = serviceResult.Message ?? "No se pudo crear el resumen.",
                ResponseData = summary
            });
        }
        catch (Exception ex)
        {
            // Cualquier excepción no controlada se considera error interno del servidor.
            Console.WriteLine($"[Chat][ERROR] Error creando resumen para chat {chatId}. {ex}");

            return StatusCode(500, new WebApiResponseDto<ChatSummaryDto>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno del servidor.",
                ResponseData = null
            });
        }

        // Mapeo de códigos de dominio a HTTP status
        static int MapHttpStatus(EnumSummaryResult code) => code switch
        {
            EnumSummaryResult.InvalidChatId => StatusCodes.Status400BadRequest,
            EnumSummaryResult.ChatNotFound => StatusCodes.Status404NotFound,
            EnumSummaryResult.AiCallFailed => StatusCodes.Status502BadGateway,
            EnumSummaryResult.AiBadPayload => StatusCodes.Status502BadGateway,
            EnumSummaryResult.PersistFailed => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    /// <summary>
    /// Actualiza las configuraciones de IA para un chat específico, incluyendo el estado de activación/desactivación
    /// y el motivo del cambio. Si la IA se desactiva por acción del mentor, también intenta generar un resumen
    /// de la conversación activa sin interrumpir el flujo principal en caso de error.
    /// </summary>
    /// <param name="chatId">Identificador del chat de Microsoft Teams.</param>
    /// <param name="settings">
    /// Objeto que contiene el nuevo estado de la IA (<c>true</c>/<c>false</c>) y un motivo descriptivo.
    /// </param>
    /// <returns>
    /// Un <see cref="IActionResult"/> con información del resultado de la operación,
    /// incluyendo un resumen generado automáticamente si corresponde.
    /// </returns>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("chat/{chatId}/ai-settings")]
    public async Task<IActionResult> UpdateAISettings(
        string chatId, 
        [FromBody] ChatAISettingsDto settings)
    {
        // Validación del ID del chat
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El MSTeamsChatId es requerido.",
                ResponseData = null
            });
        }

        // Validación de modelo
        if (!ModelState.IsValid || settings is null)
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Datos inválidos.",
                ResponseData = ModelState
            });
        }

        try
        {
            // Actualizar estado de IA en DB
            var updateSuccess = await _chatService.UpdateAISettingsAsync(chatId, settings.AIState, settings.AIChangeReason);

            if (!updateSuccess)
            {
                return NotFound(new WebApiResponseDto<object>
                {
                    ResponseCode = ResponseTypeCodeDto.NoData,
                    ResponseMessage = "No se encontró un chat activo.",
                    ResponseData = null
                });
            }

            // Determinar si debe generarse un resumen automático
            var shouldCreateSummary =
                settings.AIState == false &&
                string.Equals(settings.AIChangeReason?.Trim(), "Mentor", StringComparison.OrdinalIgnoreCase);

            ChatSummaryDto? summary = null;
            var summaryTriggered = shouldCreateSummary;

            if (shouldCreateSummary)
            {
                try
                {
                    // Generar un resumen al desactivar la IA por acción del mentor
                    var summaryResult = await _chatService.CreateSummaryAsync(chatId, settings.AIChangeReason);

                    if (summaryResult.Success)
                        summary = summaryResult.Data;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI-Settings] Error creando resumen tras 'Mentor': {ex}");
                    // No se lanza excepción — la respuesta principal debe continuar
                }
            }

            // Construir respuesta estándar
            var response = new
            {
                Updated = true,
                SummaryTriggered = summaryTriggered,
                SummaryCreated = summary != null,
                Summary = summary == null ? null : new
                {
                    summary.ChatId,
                    summary.Summary,
                    summary.SummaryType,
                    summary.KeyPoints,
                    summary.Escalated,
                    summary.EscalationReason,
                    summary.CreatedAt,
                    summary.CreatedBy
                }
            };

            return Ok(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Ok,
                ResponseMessage = "Ajustes de IA actualizados exitosamente.",
                ResponseData = response
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en UpdateAISettings (chat {chatId}): {ex}");

            return StatusCode(500, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno del servidor.",
                ResponseData = null
            });
        }
    }

    /// <summary>
    /// Marca un chat como leído por el mentor o usuario asociado. Esta operación actualiza el estado
    /// interno del chat en la base de datos estableciendo el flag de lectura y la fecha de actualización.
    /// </summary>
    /// <param name="chatId">Identificador del chat en Microsoft Teams.</param>
    /// <returns>
    /// Un <see cref="IActionResult"/> que indica si la operación fue exitosa o si el chat no fue encontrado.
    /// </returns>
    [Authorize]
    [Authorize(Policy = "AppOrUser")]
    [HttpPost("chat/{chatId}/read")]
    public async Task<IActionResult> MarkChatAsRead(string chatId)
    {
        // Validación del ID del chat
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El MSTeamsChatId es requerido.",
                ResponseData = null
            });
        }

        try
        {
            // Intento de actualizar el estado de lectura
            var success = await _chatService.MarkChatAsReadAsync(chatId);

            if (!success)
            {
                return NotFound(new WebApiResponseDto<object>
                {
                    ResponseCode = ResponseTypeCodeDto.NoData,
                    ResponseMessage = "No se encontró el chat.",
                    ResponseData = null
                });
            }

            // Respuesta exitosa
            return Ok(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Ok,
                ResponseMessage = "Chat marcado como leído.",
                ResponseData = new { ChatId = chatId, IsRead = true }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CHAT-READ] Error al marcar chat como leído ({chatId}): {ex}");

            return StatusCode(500, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno del servidor.",
                ResponseData = null
            });
        }
    }

    /// <summary>
    /// Envía un mensaje a nombre del mentor hacia un chat específico de Microsoft Teams.
    /// Soporta tanto <c>application/json</c> (solo texto) como <c>multipart/form-data</c>
    /// para mensajes con archivos e imágenes adjuntas.
    /// </summary>
    /// <param name="chatId">Identificador del chat en Microsoft Teams.</param>
    /// <returns>
    /// Un <see cref="IActionResult"/> con el detalle del resultado de la operación.
    /// </returns>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("chat/{chatId}/message")]
    public async Task<IActionResult> SendMessage(string chatId)
    {
        // Validación del ID del chat
        if (string.IsNullOrWhiteSpace(chatId))
        {
            Console.WriteLine("[MSG] Se intentó enviar un mensaje sin especificar chatId.");

            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El chatId es requerido.",
                ResponseData = null
            });
        }

        try
        {
            // Si el request incluye archivos, usar el flujo multipart
            if (Request.HasFormContentType)
            {
                return await HandleMultipartRequest(chatId);
            }

            // Si NO es multipart, procesar como JSON
            return await HandleJsonRequest(chatId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MSG] Error enviando mensaje en chat {chatId}: {ex}");

            return StatusCode(500, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno del servidor.",
                ResponseData = null
            });
        }
    }

    /// <summary>
    /// Procesa solicitudes multipart/form-data enviadas al endpoint de envío de mensajes.
    /// Permite combinar texto y archivos, sube los archivos a OneDrive mediante Graph
    /// y envía un mensaje limpio a Teams utilizando <see cref="MicrosoftGraphService"/>.
    /// </summary>
    /// <param name="chatId">Identificador del chat en una conversación de Teams.</param>
    /// <returns>
    /// Respuesta detallada con ID del mensaje enviado, archivos procesados y metadatos.
    /// </returns>
    private async Task<IActionResult> HandleMultipartRequest(string chatId)
    {
        // Leer el formulario multipart
        var form = await Request.ReadFormAsync();

        // Texto del usuario (si existe)
        var content = form["content"].ToString() ?? "";

        // Archivos adjuntos enviados desde el frontend
        var files = form.Files;

        // Flags para determinar si hay contenido visible o archivos
        var hasContent = !string.IsNullOrWhiteSpace(content);
        var hasFiles = files != null && files.Count > 0;

        // Validación mínima: debe existir contenido o archivos
        if (!hasContent && !hasFiles)
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Debe enviar contenido en 'content' o al menos un archivo.",
                ResponseData = null
            });
        }

        // Opcional: metadata enviada por el frontend

        var senderRole = form["senderRole"].ToString();
        var contentType = form["contentType"].ToString();

        var uploadedFiles = new List<UploadFileResponse>();

        // Procesamiento de archivos adjuntos (si existen)
        if (hasFiles)
        {
            foreach (var file in files)
            {
                var upload = await _microsoftGraphService.UploadFileToGraphAsync(chatId, file);

                if (!upload.Success)
                {
                    Console.WriteLine($"[MULTIPART] Error subiendo archivo {file.FileName}: {upload.ErrorMessage}");

                    return BadRequest(new WebApiResponseDto<object>
                    {
                        ResponseCode = ResponseTypeCodeDto.Error,
                        ResponseMessage = $"Error subiendo {file.FileName}: {upload.ErrorMessage}",
                        ResponseData = null
                    });
                }

                uploadedFiles.Add(upload);
            }
        }

        // Enviar mensaje limpio (solo lo que el usuario escribió)
        var result = await _microsoftGraphService.SendCleanMessageWithAttachmentsAsync(
            chatId,
            hasContent ? content : null, // Solo pasar contenido si el usuario lo escribió
            uploadedFiles
        );

        if (!result.Success)
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = result.ErrorMessage,
                ResponseData = null
            });
        }

        var responsePayload = new
        {
            MessageId = result.MessageId,
            FilesUploaded = uploadedFiles.Count,
            SentAt = result.SentAt,
            HasVisibleText = hasContent,
            Files = uploadedFiles.Select(u => new
            {
                u.FileName,
                u.Size,
                u.FileUrl
            }).ToList()
        };

        return Ok(new WebApiResponseDto<object>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Mensaje enviado con éxito.",
            ResponseData = responsePayload
        });
    }

    /// <summary>
    /// Procesa solicitudes JSON enviadas al endpoint de envío de mensajes.
    /// Permite enviar texto y adjuntos referenciados ya subidos a OneDrive.
    /// </summary>
    /// <param name="chatId">Identificador del chat al que se enviará el mensaje.</param>
    /// <returns>
    /// Acción HTTP que contiene el resultado del envío, incluyendo el ID del mensaje.
    /// </returns>
    private async Task<IActionResult> HandleJsonRequest(string chatId)
    {
        // Intentar deserializar el body JSON
        var request = await Request.ReadFromJsonAsync<SendMessageRequest>();

        if (request == null)
        {
            Console.WriteLine("[JSON-MSG] JSON inválido o vacío.");

            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Body JSON inválido.",
                ResponseData = null
            });
        }

        // Flags de contenido
        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasAttachments = request.Attachments != null && request.Attachments.Count > 0;

        if (!hasContent && !hasAttachments)
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Debe enviar contenido en 'Content' o al menos un adjunto.",
                ResponseData = null
            });
        }

        // Valores por defecto enviados por el frontend
        request.SenderRole ??= "Mentor";
        request.ContentType ??= "html";

        // Llamar al servicio de Graph para enviar mensaje limpio
        var result = await _microsoftGraphService.SendMessageToTeamsAsync(chatId, request);

        if (!result.Success)
        {
            Console.WriteLine($"[JSON-MSG] Error enviando mensaje: {result.ErrorMessage}");

            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = result.ErrorMessage,
                ResponseData = null
            });
        }

        return Ok(new WebApiResponseDto<object>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Mensaje enviado con éxito.",
            ResponseData = new
            {
                MessageId = result.MessageId,
                SentAt = result.SentAt
            }
        });
    }
}
