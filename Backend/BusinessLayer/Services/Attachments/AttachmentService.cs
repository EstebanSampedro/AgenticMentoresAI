using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Azure;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Attachments;

/// <summary>
/// Servicio encargado de recuperar archivos adjuntos almacenados en OneDrive o SharePoint 
/// utilizando Microsoft Graph.
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly GraphServiceClient _graph;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de adjuntos.
    /// </summary>
    /// <param name="graph">Instancia del cliente de Microsoft Graph</param>
    public AttachmentService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Obtiene un archivo adjunto desde OneDrive o SharePoint usando su DriveId e ItemId.
    /// </summary>
    /// <param name="driveId">Identificador del drive (OneDrive o SharePoint).</param>
    /// <param name="itemId">Identificador del elemento (archivo) dentro del drive.</param>
    /// <returns>
    /// Un <see cref="GraphFileResponse"/> con el contenido del archivo si existe y se puede acceder,
    /// o <c>null</c> si el archivo no existe, no se tienen permisos o ocurre un error controlado.
    /// </returns>
    public async Task<GraphFileResponse?> GetAttachmentAsync(string driveId, string itemId)
    {
        try
        {
            // Recupera metadatos básicos del archivo (id, nombre y tipo MIME) desde OneDrive/SharePoint.
            // Solo se seleccionan los campos necesarios para optimizar la respuesta y reducir el payload
            var driveItem = await _graph
                .Drives[driveId]
                .Items[itemId]
                .GetAsync(cfg =>
                {
                    cfg.QueryParameters.Select = new[] { "id", "fileName", "file" };
                });

            // Si el archivo no existe o no se tienen permisos, se retorna null para que el consumidor lo maneje
            if (driveItem == null) return null;

            // Asigna nombre y tipo MIME, estableciendo valores por defecto si el archivo no los proporciona
            var fileName = driveItem.Name ?? "file";
            var contentType = driveItem.File?.MimeType ?? "application/octet-stream";

            // Descarga el contenido binario del archivo
            // Esta llamada usa streaming directo desde Microsoft Graph, evitando cargar en memoria si no es necesario
            var stream = await _graph.Drives[driveId].Items[itemId].Content.GetAsync();

            // Si no se obtiene el stream (por error o acceso denegado), se retorna null
            if (stream == null) return null;

            return new GraphFileResponse
            {
                Stream = stream,
                ContentType = contentType,
                FileName = fileName
            };
        }
        catch (ODataError ex)
        {
            // Error típico del SDK de Graph (permisos, archivo no encontrado, etc.)
            Console.WriteLine($"[Attachment][Graph ODataError] No se pudo obtener el archivo {itemId} del drive {driveId}. {ex.Error?.Message}");
            return null;
        }
        catch (RequestFailedException ex)
        {
            // Error de autenticación o credenciales con Azure AD.
            Console.WriteLine($"[Attachment][Auth Error] Error de autenticación al acceder a Microsoft Graph: {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            // Error al leer o transmitir el stream (pérdida de conexión o corrupción del archivo).
            Console.WriteLine($"[Attachment][IO Error] Error de lectura del archivo {itemId}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Captura general de cualquier otro error no previsto.
            Console.WriteLine($"[Attachment][Unexpected Error] Error inesperado al procesar el archivo {itemId}: {ex}");
            return null;
        }
    }
}
