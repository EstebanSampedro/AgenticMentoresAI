using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.AI;

/// <summary>
/// Define las operaciones necesarias para interactuar con los
/// agentes de Inteligencia Artificial, incluyendo generación de texto,
/// análisis de imágenes y generación de resúmenes.
/// </summary>
public interface IaiClientService
{
    /// <summary>
    /// Envía un mensaje al agente IA para obtener una respuesta generada mediante LLM.
    /// Los parámetros se envían en el cuerpo como JSON.
    /// </summary>
    /// <param name="request">
    /// Contiene el mensaje del usuario, contexto de sesión y
    /// datos relevantes del estudiante.
    /// </param>
    /// <returns>
    /// Un objeto <see cref="AgentResponse"/> con la respuesta generada por el agente,
    /// o null si ocurre un error o la API retorna estado no exitoso.
    /// </returns>
    Task<AgentResponse?> CallTextAgentAsync(AgentRequest request);

    /// <summary>
    /// Realiza el envío de una o más imágenes a la IA para analizar su contenido.
    /// </summary>
    /// <param name="imageUrls">
    /// Lista de URLs que apuntan a las imágenes almacenadas en el backend,
    /// protegidas mediante token de aplicación.
    /// </param>
    /// <param name="chatId">
    /// Identificador de sesión que agrupa el contexto conversacional.
    /// </param>
    /// <returns>
    /// Lista de <see cref="ImageAnalysisResponse"/> con los resultados del análisis.
    /// Si ocurre cualquier error, se retornan solo las respuestas válidas obtenidas.
    /// </returns>
    Task<List<ImageAnalysisResponse>> CallImagesAgentAsync(List<string> imageUrls, string chatId);

    /// <summary>
    /// Solicita al agente IA un resumen del diálogo existente dentro de una sesión,
    /// utilizando como parámetro el chatId.
    /// </summary>
    /// <param name="chatId">
    /// Identificador de la conversación activa que debe resumirse.
    /// </param>
    /// <returns>
    /// Un objeto <see cref="SummaryApiResponse"/> con el resumen de la conversación,
    /// o null si la llamada falla o la API retorna error.
    /// </returns>
    Task<SummaryApiResponse?> CallSummaryAgentAsync(string chatId);
}
