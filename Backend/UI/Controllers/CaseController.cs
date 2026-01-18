using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Cases;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

/// <summary>
/// Controlador responsable de exponer operaciones relacionadas con casos de Salesforce.
/// 
/// Ofrece endpoints para obtener el último caso registrado de un usuario
/// y para crear nuevos casos basados en la información académica y operativa
/// de estudiantes y mentores.
/// </summary>
[Route("api")]
[ApiController]
public class CaseController : ControllerBase
{
    private readonly ICaseService _caseService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de casos.
    /// </summary>
    /// <param name="caseService">Servicio encargado de consultar y crear casos en Salesforce.</param>
    /// <param name="configuration">Acceso a la configuración de la aplicación, incluyendo URLs de Salesforce.</param>
    public CaseController(ICaseService caseService, IConfiguration configuration)
    {
        _caseService = caseService;
        _configuration = configuration;
    }

    /// <summary>
    /// Obtiene el último caso registrado en Salesforce para un usuario identificado por su BannerId.
    /// 
    /// Este endpoint se utiliza para recuperar el caso más reciente asociado a un estudiante o mentor,
    /// lo cual es necesario para determinar continuaciones de seguimiento o generar nuevos casos derivados.
    /// </summary>
    /// <param name="bannerId">Identificador Banner del usuario cuyo caso se desea consultar.</param>
    /// <returns>
    /// Un objeto con la información del último caso encontrado, o un mensaje de error si no existen registros.
    /// </returns>
    /// <remarks>
    /// Requiere autenticación y validación de acceso mediante <see cref="UserAccessFilter"/>.
    /// </remarks>
    /// <response code="200">Último caso obtenido con éxito.</response>
    /// <response code="400">El parámetro <paramref name="bannerId"/> no fue proporcionado.</response>
    /// <response code="401">El usuario no está autenticado.</response>
    /// <response code="403">El usuario autenticado no tiene permisos para esta operación.</response>
    /// <response code="404">No se encontró ningún caso asociado al BannerId especificado.</response>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("case/last")]
    public async Task<IActionResult> GetLastCaseByUserAsync([FromQuery] string? bannerId)
    {
        // Valida datos de entrada
        if (string.IsNullOrWhiteSpace(bannerId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Debe especificar 'bannerId' para consultar el último caso.",
                ResponseData = null
            });
        }

        var lastCase = await _caseService.GetLastCaseByUserAsync(bannerId);

        // Valida el resultado
        if (lastCase is null)
        {
            return NotFound(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontró ningún caso para el usuario especificado.",
                ResponseData = null
            });
        }

        // Construye la URL de Salesforce
        var baseUrl = _configuration["Salesforce:BaseUrl"]?.TrimEnd('/');
        var caseUrl = (!string.IsNullOrWhiteSpace(lastCase.Id) && !string.IsNullOrWhiteSpace(baseUrl))
            ? $"{baseUrl}/lightning/r/Case/{lastCase.Id}/view"
            : null;

        // Prepara la respuesta de datos
        var data = new LastCaseDto(
            Id: lastCase.Id,
            CaseNumber: lastCase.CaseNumber,
            Subject: lastCase.Subject,
            Status: lastCase.Status,
            Priority: lastCase.Priority,
            Origin: lastCase.Origin,
            OwnerName: lastCase.OwnerName,
            OwnerEmail: lastCase.OwnerEmail,
            CreatedDate: lastCase.CreatedDate,
            LastModifiedDate: lastCase.LastModifiedDate,
            SalesforceUrl: caseUrl,
            QueryBannerId: bannerId
        );

        // Devuelve respuesta exitosa
        return Ok(new WebApiResponseDto<LastCaseDto>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Último caso obtenido con éxito.",
            ResponseData = data
        });
    }

    /// <summary>
    /// Crea un nuevo caso en Salesforce basado en la información proporcionada por un mentor
    /// y utilizando datos del último caso previo para continuar el seguimiento académico.
    /// 
    /// Este endpoint construye el payload necesario para Salesforce, valida la respuesta
    /// y devuelve un objeto con la información del caso recién creado.
    /// </summary>
    /// <param name="caseRequest">Objeto con los datos requeridos para crear un caso en Salesforce.</param>
    /// <returns>
    /// Un objeto con los datos del caso recién creado, o un error si Salesforce rechaza la solicitud.
    /// </returns>
    /// <remarks>
    /// Requiere autenticación y validación mediante <see cref="UserAccessFilter"/>.
    /// </remarks>
    /// <response code="200">Caso creado correctamente.</response>
    /// <response code="400">La solicitud contiene datos inválidos o incompletos.</response>
    /// <response code="401">El usuario no está autenticado.</response>
    /// <response code="403">El usuario autenticado no tiene permisos para crear casos.</response>
    /// <response code="502">Salesforce devolvió un error de validación o procesamiento.</response>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("case")]
    public async Task<IActionResult> CreateCaseAsync([FromBody, Required] CreateCaseRequest caseRequest)
    {
        // Valida el cuerpo de la solicitud
        if (!ModelState.IsValid)
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Solicitud inválida. Verifique los datos enviados.",
                ResponseData = null
            });
        }

        // Obtiene el último caso del mentor
        var lastCase = await _caseService.GetLastCaseByUserAsync(caseRequest.BannerMentor);

        // Crea nuevo caso en Salesforce
        var salesforceResponse = await _caseService.CreateCaseAsync(
            bannerStudent: caseRequest.BannerStudent,
            bannerMentor: caseRequest.BannerMentor,
            ownerEmail: caseRequest.OwnerEmail ?? string.Empty,
            subject: lastCase?.Subject ?? string.Empty,
            theme: caseRequest.Theme,
            summary: caseRequest.Summary,                     
            nextDate: caseRequest.NextDate
        );

        // Valida si Salesforce devolvió un error controlado (por ejemplo, validación fallida)
        if (salesforceResponse == null || !salesforceResponse.Success)
        {
            var message = (salesforceResponse?.Errors is { Count: > 0 })
                ? string.Join(" | ", salesforceResponse.Errors.Select(e => e?.ToString()))
                : "Error desconocido al crear el caso en Salesforce.";

            // Error lógico desde Salesforce
            return StatusCode(StatusCodes.Status502BadGateway, new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = message,
                ResponseData = null
            });
        }

        // Construye URL directa al caso en Salesforce
        var baseUrl = _configuration["Salesforce:BaseUrl"]?.TrimEnd('/');
        var caseUrl = (!string.IsNullOrWhiteSpace(salesforceResponse.Id) && !string.IsNullOrWhiteSpace(baseUrl))
            ? $"{baseUrl}/lightning/r/Case/{salesforceResponse.Id}/view"
            : null;

        // Prepara datos de respuesta
        var data = new CreateCaseDto(
            CaseId: salesforceResponse.Id,
            SalesforceUrl: caseUrl,
            BannerStudent: caseRequest.BannerStudent,
            BannerMentor: caseRequest.BannerMentor,
            OwnerEmail: caseRequest.OwnerEmail,
            NextDate: caseRequest.NextDate,
            Status: _configuration["Salesforce:Case:Status"] ?? "No Atendido",
            Priority: _configuration["Salesforce:CaseDefaults:Priority"] ?? "Baja"
        );

        // Respuesta exitosa
        return Ok(new WebApiResponseDto<CreateCaseDto>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Caso creado con éxito.",
            ResponseData = data
        });
    }
}
