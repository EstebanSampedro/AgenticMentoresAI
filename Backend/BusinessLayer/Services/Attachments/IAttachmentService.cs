using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Attachments;

/// <summary>
/// Servicio responsable de obtener archivos adjuntos desde OneDrive o SharePoint
/// a través de Microsoft Graph.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Recupera un archivo adjunto desde el drive especificado.
    /// </summary>
    /// <param name="driveId">Identificador del drive (OneDrive o SharePoint).</param>
    /// <param name="itemId">Identificador del archivo dentro del drive.</param>
    /// <returns>
    /// Un objeto <see cref="GraphFileResponse"/> con el contenido del archivo,
    /// o null si no existe o no se tiene acceso.
    /// </returns>
    Task<GraphFileResponse?> GetAttachmentAsync(string driveId, string itemId);
}
