using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.AI;

/// <summary>
/// Define los métodos para interactuar con los servicios externos de IA,
/// como generación automática de resúmenes a partir de conversaciones.
/// </summary>
public interface IaiClientService
{
    /// <summary>
    /// Llama al servicio de IA encargado de generar un resumen en base a la
    /// conversación mantenida dentro de un chat específico.
    /// </summary>
    /// <param name="chatId">
    /// Identificador del chat para rastrear el contexto del resumen.
    /// No debe ser null ni contener solo espacios.
    /// </param>
    /// <param name="conversation">
    /// Texto completo del intercambio de mensajes que se enviará a la IA.
    /// Se recomienda enviar contenido no vacío y debidamente consolidado.
    /// </param>
    /// <returns>
    /// Instancia de <see cref="SummaryApiResponse"/> si la llamada es exitosa;
    /// null si ocurre un error, no se obtiene token o el servicio responde fallo.
    /// </returns>
    Task<SummaryApiResponse?> CallSummaryAgentAsync(string chatId, string conversation);
}
