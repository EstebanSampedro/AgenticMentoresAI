using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.BannerWebApi;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Users;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

[Route("api")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IBannerWebApiService _bannerWebApiService;
    private readonly IUserService _userService;
    private readonly IBackgroundTaskQueue _queue;

    public UserController(
        IUserService userService,
        IBannerWebApiService bannerWebApiService,
        IBackgroundTaskQueue queue
        )
    {
        _userService = userService;
        _bannerWebApiService = bannerWebApiService;
        _queue = queue;
    }

    /// <summary>
    /// Sincroniza los usuarios mentores y estudiantes en la base interna 
    /// desde un documento de Excel.
    /// </summary>
    [Authorize]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("user/sync/excel")]
    public async Task<IActionResult> SyncUsersFromExcel()
    {
        await _queue.EnqueueAsync(async (sp, ct) =>
        {
            var userService = sp.GetRequiredService<IUserService>();
            await userService.SyncUsersFromExcelAsync();
        });

        return Accepted(new { Message = "Sincronización encolada." });
    }

    /// <summary>
    /// Obtiene la información básica del estudiante desde Banner WebAPI usando el correo electrónico proporcionado.
    /// </summary>
    /// <param name="email">Correo institucional del estudiante.</param>
    /// <returns>
    /// Respuesta estandarizada con los datos recuperados desde Banner WebAPI.
    /// </returns>
    /// <response code="200">Los datos fueron obtenidos correctamente.</response>
    /// <response code="400">El correo proporcionado no es válido.</response>
    /// <response code="404">No se encontró información asociada al correo especificado.</response>
    [Authorize(Policy = "AppOrUser")]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("user/student-info/{email}")]
    public async Task<IActionResult> GetStudentInfo(string email)
    {
        // Se valida que el correo recibido sea válido antes de procesar la solicitud
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "El correo del estudiante es obligatorio.",
                ResponseData = null
            });
        }

        var normalizedEmail = email.Trim().ToLower();

        // Se invoca el servicio de Banner WebAPI para obtener la información básica del estudiante
        var (personId, bannerId, pidm, programs) =
            await _bannerWebApiService.GetStudentKeyInfoAsync(normalizedEmail);

        // Si no se encuentra información, se retorna una respuesta estandarizada con estado NotFound
        if (string.IsNullOrEmpty(personId))
        {
            return NotFound(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.NoData,
                ResponseMessage = "No se encontró información asociada a ese estudiante.",
                ResponseData = null
            });
        }

        // Se construye la respuesta exitosa utilizando WebApiResponseDto para mantener consistencia en la API
        return Ok(new WebApiResponseDto<object>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Datos obtenidos con éxito.",
            ResponseData = new
            {
                PersonId = personId,
                BannerId = bannerId,
                Pidm = pidm,
                Programs = programs
            }
        });
    }
}
