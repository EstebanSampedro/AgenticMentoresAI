using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

[Route("api")]
[ApiController]
public class MentorController : ControllerBase
{
    private readonly IMentorService _mentorService;
    private readonly IOboSessionOrchestrator _obo;

    private readonly IConfiguration _configuration;

    public MentorController(
        IMentorService mentorService,
        IOboSessionOrchestrator obo,
        IConfiguration configuration)
    {
        _mentorService = mentorService;
        _obo = obo;

        _configuration = configuration;
    }

    /// <summary>
    /// Obtiene el tipo de mentor para el correo indicado.  
    /// Como efecto colateral, inicializa o actualiza una sesión OBO de larga duración:
    /// - Siembra el MSAL Token Cache en el servidor.
    /// - Guarda la sessionKey asociada al usuario para operaciones Graph posteriores.
    /// </summary>
    /// <param name="email">
    /// Correo UPN del mentor que se utilizará para consultar su tipo.
    /// </param>
    /// <returns>
    /// Retorna 200 OK con un <see cref="MentorTypeDto"/>;  
    /// retorna 401 Unauthorized si falta o es inválido el token del encabezado Authorization;  
    /// retorna 500 en caso de error inesperado.
    /// </returns>
    [Authorize]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("mentor/{email}/type")]
    public async Task<IActionResult> GetMentorType(string email)
    {
        try
        {
            // Se intenta obtener el token del encabezado Authorization.
            // Este token proviene del SSO del tab de Teams (OBO origin token).
            if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                return Unauthorized(new WebApiResponseDto<object>
                {
                    ResponseCode = ResponseTypeCodeDto.Error,
                    ResponseMessage = "Encabezado Authorization faltante.",
                    ResponseData = null
                });
            }

            // Extrae la porción 'Bearer <token>' de forma segura
            var token = authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.ToString().Substring("Bearer ".Length).Trim()
                : null;

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new WebApiResponseDto<object>
                {
                    ResponseCode = ResponseTypeCodeDto.Error,
                    ResponseMessage = "No se encontró el token Bearer en el encabezado.",
                    ResponseData = null
                });
            }

            // Inicializa el Long-running OBO.
            // Esto permite que MSAL genere y almacene tokens de largo plazo (RT/AT)
            // y obtenga una sessionKey usada después para enviar mensajes vía Graph a nombre del mentor.
            await _obo.BootstrapAsync(token!, createdByHint: email);

            // Obtiene el tipo de mentor desde la base de datos.
            // El método también almacena el token recibido en KeyVault según la implementación interna.
            var mentorTypeDto = await _mentorService.GetMentorTypeByEmailAsync(email, token);

            // Valida los chatsId del mentor que están como null y envía un mensaje proactivo
            // para generar un chatId para la conversación
            //if(result is not null)
            //{
            //    _ = Task.Run(async () =>
            //    {
            //        try
            //        {
            //            await _mentorService.SendProactiveMessagesIfNecessaryAsync(email, token);
            //        }
            //        catch (Exception ex)
            //        {
            //            Console.WriteLine($"Error al enviar mensaje proactivo: {ex.Message}");
            //        }
            //    });
            //}

            // Respuesta estándar: si no existe el mentor, se retorna MentorType = null.
            return Ok(new WebApiResponseDto<MentorTypeDto>
            {
                ResponseCode = ResponseTypeCodeDto.Ok,
                ResponseMessage = "Tipo de mentor obtenido con éxito",
                ResponseData = mentorTypeDto
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetMentorType] Error procesando solicitud para {email}: {ex}");

            return StatusCode(500, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno del servidor.",
                ResponseData = null
            });
        }
    }

    /// <summary>
    /// Recupera la lista de estudiantes que mantienen conversaciones activas con el mentor indicado.
    /// También inicializa/actualiza la sesión OBO de larga duración mediante el token recibido.
    /// </summary>
    /// <param name="mentorEmail">Correo institucional (UPN) del mentor.</param>
    /// <returns>
    /// Un objeto estándar <see cref="WebApiResponseDto{T}"/> que contiene la lista de estudiantes,
    /// o un mensaje de error si no se pudo procesar la solicitud.
    /// </returns>
    [Authorize]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("mentor/{mentorEmail}/students")]
    public async Task<ActionResult<List<StudentChatDto>>> GetStudentsByMentor(string mentorEmail)
    {
        // Validación básica del parámetro
        if (string.IsNullOrWhiteSpace(mentorEmail))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El correo del mentor es requerido.",
                ResponseData = null
            });
        }

        // Intentar obtener el encabezado Authorization
        if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Unauthorized(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Encabezado Authorization faltante.",
                ResponseData = null
            });
        }

        // Extraer token Bearer
        var token = authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader.ToString().Substring("Bearer ".Length).Trim()
            : null;

        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "No se encontró el token Bearer en el encabezado.",
                ResponseData = null
            });
        }

        // Obtener estudiantes asociados al mentor.
        // Internamente esto persiste el token en KeyVault.
        var students = await _mentorService.GetStudentsByMentorEmailAsync(mentorEmail, token);

        if (students == null || !students.Any())
        {
            return NotFound(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontraron estudiantes asociados al mentor.",
                ResponseData = new List<StudentChatDto>()
            });
        }

        return Ok(new WebApiResponseDto<List<StudentChatDto>>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Estudiantes obtenidos con éxito.",
            ResponseData = students
        });
    }

    /// <summary>
    /// Busca mensajes según un texto en todas las conversaciones entre un mentor y sus estudiantes.
    /// Soporta paginación y genera automáticamente la URL de la siguiente página.
    /// </summary>
    /// <param name="mentorEmail">Correo del mentor usado como filtro principal.</param>
    /// <param name="request">Criterios de búsqueda: texto, página y tamaño de página.</param>
    /// <returns>
    /// Respuesta paginada de mensajes y URL para la siguiente página, si existe.
    /// </returns>
    [Authorize]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("mentor/{mentorEmail}/messages")]
    public async Task<IActionResult> SearchMessages(string mentorEmail, [FromQuery] SearchMessagesRequest request)
    {
        // Validación del modelo de entrada
        if (!ModelState.IsValid)
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Solicitud inválida.",
                ResponseData = ModelState
            });
        }

        // Normalizar parámetros de paginación
        var normalizedRequest = new SearchMessagesRequest
        {
            MentorEmail = mentorEmail,
            Query = request.Query,
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 10 : request.PageSize
        };

        // Ejecutar búsqueda
        var pagedResult = await _mentorService.SearchMessagesAsync(normalizedRequest);

        // Si no hay resultados
        if (pagedResult.Total == 0 || pagedResult.Results == null || !pagedResult.Results.Any())
        {
            return NotFound(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontraron mensajes que coincidan con los criterios de búsqueda.",
                ResponseData = new List<object>()
            });
        }

        // Construcción de URL siguiente página
        string baseUrl = $"{_configuration["Backend:Endpoint"]?.TrimEnd('/')}{Request.Path}";
        string? nextPageUrl = null;

        if ((pagedResult.Page * pagedResult.PageSize) < pagedResult.Total)
        {
            nextPageUrl = $"{baseUrl}?page={pagedResult.Page + 1}&pageSize={pagedResult.PageSize}&query={Uri.EscapeDataString(request.Query ?? "")}";
        }

        // Construcción del DTO final
        var responseData = new SearchMessagesPagedDto
        {
            Page = pagedResult.Page,
            PageSize = pagedResult.PageSize,
            Total = pagedResult.Total,
            UrlNextPage = nextPageUrl,
            Results = pagedResult.Results.Select(m => new SearchMessagesItemDto
            {
                Id = m.Id,
                SenderRole = m.SenderRole,
                StudentFullName = m.StudentFullName,
                ChatId = m.ChatId,
                Content = m.Content,
                ContentType = m.ContentType,
                Date = m.Date?.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
        };

        return Ok(new WebApiResponseDto<SearchMessagesPagedDto>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Mensajes obtenidos con éxito.",
            ResponseData = responseData
        });
    }

    /// <summary>
    /// Consulta un Contact de Salesforce usando el correo electrónico proporcionado.
    /// Se busca tanto por el campo estándar <c>Email</c> como por <c>hed__UniversityEmail__c</c>.
    /// </summary>
    /// <param name="email">Correo del usuario a consultar en Salesforce.</param>
    /// <returns>
    /// Un objeto con código de respuesta, mensaje y los datos obtenidos de Salesforce.
    /// </returns>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("mentor/salesforce/{email}")]
    public async Task<IActionResult> GetSalesforceUserByEmail(string email)
    {
        // Normalizar email desde ruta (evita doble encoding desde Teams/web)
        email = Uri.UnescapeDataString(email ?? string.Empty).Trim();

        // Validación mínima para evitar llamadas innecesarias a Salesforce
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Parámetro 'email' inválido.",
                ResponseData = null
            });
        }

        try
        {
            // Llamada al servicio que consulta Salesforce
            var contact = await _mentorService.GetMentorAsync(email);

            // Si no existe resultado
            if (contact is null)
            {
                return NotFound(new WebApiResponseDto<object>
                {
                    ResponseCode = ResponseTypeCodeDto.NoData,
                    ResponseMessage = $"No se encontró un contacto en Salesforce con el correo '{email}'.",
                    ResponseData = null
                });
            }

            var record = contact.Records[0];

            var dto = new ContactSalesforceDto
            {
                Id = record.Id,
                FullName = record.Name,
                BannerId = record.Codigo_banner__c,
                PersonalEmail = record.Email,
                UniversityEmail = record.hed__UniversityEmail__c,
                Assignment = record.Asignacion__c,
                AssignedStudents = record.Estudiantes_Asignados_Actualmente_Mentor__c,
                LimitAssigned = record.Limite_Asignado__c
            };

            return Ok(new WebApiResponseDto<ContactSalesforceDto>
            {
                ResponseCode = ResponseTypeCodeDto.Ok,
                ResponseMessage = "Usuario obtenido con éxito.",
                ResponseData = dto
            });

        }
        catch (OperationCanceledException)
        {
            // Caso especial: solicitud cancelada (útil en frontends SPA)
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "La solicitud fue cancelada por el cliente.",
                ResponseData = null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error interno al consultar el usuario en Salesforce.",
                ResponseData = null
            });
        }
    }
}
