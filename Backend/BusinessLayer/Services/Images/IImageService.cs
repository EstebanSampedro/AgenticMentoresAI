using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Images;

/// <summary>
/// Define las operaciones para obtener imágenes asociadas a mensajes de Microsoft Teams,
/// específicamente aquellas almacenadas como <c>hostedContents</c> dentro de un mensaje.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Obtiene el contenido binario de un elemento <c>hostedContent</c> asociado a un mensaje de Teams,
    /// utilizando las credenciales y sesión de larga duración del mentor dueño del chat.
    /// </summary>
    /// <param name="chatId">
    /// Identificador único del chat de Microsoft Teams donde se encuentra el mensaje.
    /// </param>
    /// <param name="messageId">
    /// Identificador del mensaje que contiene el <c>hostedContent</c> solicitado.
    /// </param>
    /// <param name="contentId">
    /// Identificador del <c>hostedContent</c> cuyo contenido binario se desea obtener.
    /// </param>
    /// <returns>
    /// Un objeto <see cref="GraphImageResponse"/> con los bytes y el tipo de contenido de la imagen,
    /// o <c>null</c> si no se pudo obtener el recurso (por ejemplo, si la sesión de larga duración
    /// no es válida, no existe, o si la operación resulta no autorizada).
    /// </returns>
    Task<GraphImageResponse?> GetHostedContentImageAsync(string chatId, string messageId, string contentId);
}
