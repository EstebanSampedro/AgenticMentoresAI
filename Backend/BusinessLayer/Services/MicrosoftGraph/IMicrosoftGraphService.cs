using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Microsoft.AspNetCore.Http;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.MicrosoftGraph;

/// <summary>
/// Define las operaciones disponibles para interactuar con Microsoft Graph,
/// incluyendo autenticación OBO, envío de mensajes a Teams, subida de archivos
/// a OneDrive y resolución de identidades de usuarios.
/// </summary>
public interface IMicrosoftGraphService
{
    /// <summary>
    /// Obtiene el Entra ID de un usuario a partir de su email.
    /// Usa Graph.Users[email] con los permisos del remitente.
    /// </summary>
    /// <param name="email">Correo electrónico a consultar.</param>
    /// <returns>
    /// El identificador Entra ID del usuario si existe; de lo contrario <c>null</c>.
    /// </returns>
    Task<string?> GetEntraUserIdByEmailAsync(string email);

    /// <summary>
    /// Envía un mensaje proactivo a un usuario de Teams usando un chat 1:1.
    /// Crea el chat si no existe y envía el primer mensaje.
    /// </summary>
    /// <param name="mentorEntraId">ObjectId del mentor remitente.</param>
    /// <param name="studentEntraId">ObjectId del estudiante destino.</param>
    /// <param name="messageContent">Contenido HTML del mensaje a enviar.</param>
    /// <returns>
    /// El Id del chat creado o existente; <c>null</c> si falla.
    /// </returns>
    Task<string?> SendMessageToUserAsync(
        string mentorEntraId,
        string studentEntraId,
        string messageContent);

    /// <summary>
    /// Busca si existe un chat 1:1 entre dos usuarios específicos en Teams.
    /// </summary>
    /// <param name="mentorId">Entra ID del mentor.</param>
    /// <param name="studentId">Entra ID del estudiante.</param>
    /// <returns>
    /// El Id del chat si existe, o <c>null</c> si no se encuentra.
    /// </returns>
    Task<string?> GetOneOnOneChatIdAsync(string mentorId, string studentId);

    /// <summary>
    /// Envía un mensaje al chat de Teams indicado, con texto y opcionalmente attachments.
    /// El mensaje se envía actuando en nombre del mentor mediante OBO de larga duración.
    /// </summary>
    /// <param name="chatId">Identificador del chat de Teams.</param>
    /// <param name="request">Contenido y attachments del mensaje.</param>
    /// <returns>
    /// Un objeto <see cref="SendMessageResponse"/> con el estado y detalles del mensaje enviado.
    /// </returns>
    Task<SendMessageResponse> SendMessageToTeamsAsync(
        string chatId,
        SendMessageRequest request);

    /// <summary>
    /// Envía un mensaje limpio al chat indicado, incluyendo texto opcional
    /// y archivos previamente subidos a OneDrive.
    /// </summary>
    /// <param name="chatId">Identificador del chat de Teams.</param>
    /// <param name="userContent">Contenido HTML proporcionado por el mentor.</param>
    /// <param name="uploadedFiles">Lista de archivos ya subidos a OneDrive.</param>
    /// <returns>
    /// Objeto <see cref="SendMessageResponse"/> con resultados de la operación.
    /// </returns>
    Task<SendMessageResponse> SendCleanMessageWithAttachmentsAsync(
        string chatId,
        string? userContent,
        List<UploadFileResponse> uploadedFiles);

    /// <summary>
    /// Sube un archivo al OneDrive del mentor asociado al chat especificado.
    /// Maneja uploads pequeños y grandes, sanitiza nombres y genera enlaces compartibles.
    /// </summary>
    /// <param name="chatId">Identificador del chat en Teams usado para resolver al mentor.</param>
    /// <param name="file">Archivo recibido del frontend.</param>
    /// <returns>
    /// Un <see cref="UploadFileResponse"/> con URL del archivo, nombre final y metadatos.
    /// </returns>
    Task<UploadFileResponse> UploadFileToGraphAsync(string chatId, IFormFile file);
}
