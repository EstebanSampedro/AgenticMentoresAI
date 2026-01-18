using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Students;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

/// <summary>
/// Controlador encargado de exponer operaciones relacionadas con la obtención
/// de información contextual de estudiantes.
/// </summary>
[ApiController]
[Route("api")]
public class StudentController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    /// <summary>
    /// Obtiene la información contextual del estudiante basado en el correo proporcionado.
    /// </summary>
    /// <param name="email">Correo institucional o personal del estudiante.</param>
    /// <returns>
    /// Respuesta estándar con los datos del estudiante si existe,
    /// o un mensaje indicando que no se encontró información.
    /// </returns>
    /// <response code="200">Información encontrada y retornada correctamente.</response>
    /// <response code="400">El correo proporcionado es inválido.</response>
    /// <response code="404">No se encontró información para el correo especificado.</response>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("student/{email}")]
    public async Task<IActionResult> GetStudentContext(string email)
    {
        // Se valida que el parámetro email sea válido antes de procesar la solicitud
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El correo del estudiante es obligatorio.",
                ResponseData = null
            });
        }

        // Se consulta el servicio para obtener la información contextual del estudiante
        var contextInfo = await _studentService.GetStudentContextByEmailAsync(email);

        // Si no existe información, se retorna una respuesta NotFound estandarizada
        if (contextInfo == null)
        {
            return NotFound(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontró información para el estudiante con ese correo.",
                ResponseData = null
            });
        }

        // Se construye la respuesta exitosa utilizando el formato WebApiResponseDto
        return Ok(new WebApiResponseDto<StudentContextDto>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Información del estudiante obtenida exitosamente.",
            ResponseData = contextInfo
        });
    }
}
