using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Images;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

/// <summary>
/// Controlador encargado de exponer endpoints para obtener imágenes relacionadas con mensajes de Microsoft Teams,
/// específicamente aquellas almacenadas como <c>hostedContents</c>.
/// </summary>
[Route("api")]
[ApiController]
public class ImageController : ControllerBase
{
    private readonly IImageService _imageService;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de imágenes.
    /// </summary>
    /// <param name="imageService">
    /// Servicio responsable de recuperar el contenido binario de imágenes almacenadas como <c>hostedContents</c> en Teams.
    /// </param>
    public ImageController(IImageService imageService)
    {
        _imageService = imageService;
    }

    /// <summary>
    /// Obtiene la imagen asociada a un <c>hostedContent</c> perteneciente a un mensaje de Microsoft Teams.
    /// </summary>
    /// <param name="chatId">Identificador del chat donde se encuentra el mensaje.</param>
    /// <param name="messageId">Identificador del mensaje que contiene el <c>hostedContent</c>.</param>
    /// <param name="contentId">Identificador del elemento <c>hostedContent</c> cuyo contenido se desea obtener.</param>
    /// <returns>
    /// Un archivo binario con el contenido de la imagen si existe y es accesible.
    /// Retorna <see cref="NotFoundResult"/> si no se encuentra o no puede obtenerse.
    /// </returns>
    /// <remarks>
    /// Este endpoint requiere autorización y validación de acceso mediante <see cref="UserAccessFilter"/>.
    /// El contenido se devuelve utilizando el tipo MIME indicado por Microsoft Graph.
    /// </remarks>
    //[Authorize]
    //[ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("images/{chatId}/{messageId}/{contentId}")]
    public async Task<IActionResult> GetImage(string chatId, string messageId, string contentId)
    {
        // Solicita al servicio la imagen del hostedContent
        var result = await _imageService.GetHostedContentImageAsync(chatId, messageId, contentId);

        // Si no se encontró o no se pudo recuperar, se devuelve NotFound.
        if (result == null)
            return NotFound();

        // Devuelve el archivo binario con el tipo MIME correspondiente.
        return File(result.Content, result.ContentType);
    }
}
