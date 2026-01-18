using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.ClientLogs;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

[Route("api")]
[ApiController]
public sealed class ClientLogsController : ControllerBase
{
    private readonly IClientLogSink _sink;

    /// <summary>
    /// Inicializa el controlador encargado de recibir y almacenar los logs enviados por el frontend.
    /// </summary>
    /// <param name="sink">
    /// Implementación que define el destino y la forma de persistencia de los registros enviados por el frontend.
    /// </param>
    public ClientLogsController(IClientLogSink sink) => _sink = sink;

    /// <summary>
    /// Recibe un log individual enviado desde el frontend y lo envía al sink correspondiente
    /// para su procesamiento o almacenamiento.
    /// </summary>
    /// <param name="entry">Objeto que contiene la información del log generado en el cliente.</param>
    /// <param name="ct">Token para cancelar la operación si es necesario.</param>
    /// <returns>
    /// Retorna Accepted si el log fue recibido y enviado al sink correctamente.
    /// Retorna ValidationProblem si el modelo enviado presenta errores.
    /// </returns>
    [Authorize]
    [ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("client-logs")]
    public async Task<IActionResult> Post([FromBody] ClientLogEntry entry)
    {
        // Verifica que el contenido enviado por el frontend cumpla con las reglas de validación
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        // Envía el log al sink que se encargará de almacenarlo o procesarlo
        await _sink.WriteAsync(entry);

        // Se devuelve un Accepted para indicar que el log fue recibido correctamente
        return Accepted();
    }
}
