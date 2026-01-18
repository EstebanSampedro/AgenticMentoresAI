using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Attachments;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

/// <summary>
/// Controlador responsable de exponer endpoints para obtener archivos adjuntos
/// almacenados en OneDrive o SharePoint mediante Microsoft Graph.
/// 
/// Este controlador actúa únicamente como capa de transporte,
/// delegando la lógica de recuperación de archivos al <see cref="IAttachmentService"/>.
/// </summary>
[Route("api")]
[ApiController]
public class AttachmentController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de adjuntos.
    /// </summary>
    /// <param name="attachmentService">Servicio encargado de recuperar archivos desde Microsoft Graph.</param>
    public AttachmentController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    /// <summary>
    /// Recupera un archivo adjunto almacenado en OneDrive o SharePoint,
    /// identificándolo mediante el <paramref name="driveId"/> y el <paramref name="itemId"/>.
    /// 
    /// El archivo se devuelve como contenido binario para descarga directa.
    /// </summary>
    /// <param name="driveId">Identificador del drive de OneDrive o SharePoint que contiene el archivo.</param>
    /// <param name="itemId">Identificador del archivo dentro del drive.</param>
    /// <returns>
    /// Un archivo descargable si el recurso existe, o un error adecuado en caso contrario.
    /// </returns>
    /// <remarks>
    /// Este endpoint requiere autenticación y validación mediante <see cref="UserAccessFilter"/>.
    /// </remarks>
    /// <response code="200">Devuelve el archivo correctamente.</response>
    /// <response code="400">Los parámetros de entrada son inválidos.</response>
    /// <response code="401">El usuario no está autenticado.</response>
    /// <response code="403">El usuario autenticado no tiene permisos para acceder al recurso.</response>
    /// <response code="404">El archivo solicitado no existe o no se pudo recuperar.</response>
    [Authorize]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("attachments/{driveId}/{itemId}")]
    public async Task<IActionResult> GetAttachment(string driveId, string itemId)
    {
        // Valida parámetros de entrada antes de procesar
        if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(itemId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Los parámetros 'driveId' e 'itemId' son requeridos.",
                ResponseData = null
            });
        }

        // Llama al servicio que obtiene el archivo desde Microsoft Graph
        var result = await _attachmentService.GetAttachmentAsync(driveId, itemId);

        // Valida si no se encontró el archivo
        if (result == null)
        {
            return NotFound(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontró el archivo solicitado.",
                ResponseData = null
            });
        }

        // Si el servicio retorna un stream (descarga directa)
        if (result.Stream != null)
            return File(result.Stream, result.ContentType, result.FileName);

        // Si el contenido está en memoria (por ejemplo, archivo pequeño en base64)
        return File(result.Content!, result.ContentType, result.FileName);
    }
}
