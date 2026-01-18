using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.ClientLogs;

/// <summary>
/// Define el contrato para los destinos donde se enviarán los logs generados por el frontend.
/// Un sink representa un punto final de almacenamiento o procesamiento de los registros.
/// </summary>
public interface IClientLogSink
{
    /// <summary>
    /// Procesa un registro enviado por el cliente.
    /// </summary>
    /// <param name="entry">Entrada de log proveniente del frontend.</param>
    /// <returns>
    /// Una tarea que representa la operación asincrónica de escritura del log.
    /// </returns>
    Task WriteAsync(ClientLogEntry entry);
}
