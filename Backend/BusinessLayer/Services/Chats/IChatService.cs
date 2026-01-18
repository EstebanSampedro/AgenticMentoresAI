using Academikus.AgenteInteligenteMentoresWebApi.Entity.DTOs;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Chats;

/// <summary>
/// Define las operaciones relacionadas con chats, mensajes, resúmenes,
/// estados de IA y envío de contenido hacia Microsoft Teams.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Recupera un conjunto paginado de mensajes pertenecientes a un chat específico.
    /// </summary>
    /// <param name="chatId">Identificador del chat en el sistema.</param>
    /// <param name="page">Número de página solicitada.</param>
    /// <param name="pageSize">Cantidad de mensajes por página.</param>
    /// <param name="query">Texto opcional para filtrar mensajes por contenido.</param>
    /// <returns>
    /// Una tupla que contiene la lista de mensajes y el total de mensajes disponibles.
    /// </returns>
    Task<(List<ChatMessageModel> Messages, int TotalCount)> GetMessagesByChatIdAsync(
        string chatId,
        int page,
        int pageSize,
        string query
    );

    /// <summary>
    /// Genera un resumen de la conversación actual del chat utilizando el tipo de resumen solicitado.
    /// </summary>
    /// <param name="chatId">ID del chat a resumir.</param>
    /// <param name="type">Tipo de resumen solicitado.</param>
    /// <returns>
    /// Resultado del proceso de resumen, incluyendo el contenido generado y el estado del proceso.
    /// </returns>
    Task<ServiceResult<ChatSummaryDto>> CreateSummaryAsync(string chatId, string type);

    /// <summary>
    /// Genera un resumen diario para la conversación actual del chat.
    /// </summary>
    /// <param name="chatId">ID del chat a resumir.</param>
    /// <param name="type">Tipo asociado al resumen diario.</param>
    /// <returns>
    /// Resultado del proceso de resumen diario y los datos generados.
    /// </returns>
    Task<ServiceResult<ChatSummaryDto>> CreateDailySummaryAsync(string chatId, string type);

    /// <summary>
    /// Obtiene resúmenes históricos asociados a un chat aplicando paginación.
    /// </summary>
    /// <param name="chatId">Identificador del chat.</param>
    /// <param name="page">Número de página solicitado.</param>
    /// <param name="pageSize">Cantidad de resúmenes por página.</param>
    /// <returns>
    /// Una tupla que contiene la lista de resúmenes y el total disponible.
    /// </returns>
    Task<(List<ChatSummaryDto> Summaries, int TotalCount)> GetSummariesByChatIdAsync(
        string chatId,
        int page,
        int pageSize
    );

    /// <summary>
    /// Actualiza el estado de habilitación de la IA para un chat y registra el motivo del cambio.
    /// </summary>
    /// <param name="chatId">ID del chat a actualizar.</param>
    /// <param name="aiState">Nuevo estado de la IA en el chat.</param>
    /// <param name="aiChangeReason">Motivo asociado al cambio de estado.</param>
    /// <returns>
    /// True si el cambio se aplicó correctamente; de lo contrario, false.
    /// </returns>
    Task<bool> UpdateAISettingsAsync(string chatId, bool aiState, string aiChangeReason);

    /// <summary>
    /// Marca un chat como leído por parte del mentor.
    /// </summary>
    /// <param name="chatId">ID del chat que será marcado como leído.</param>
    /// <returns>
    /// True si el chat fue actualizado; de lo contrario, false.
    /// </returns>
    Task<bool> MarkChatAsReadAsync(string chatId);

    /// <summary>
    /// Recupera mensajes ubicados antes y después de un mensaje específico para proporcionar contexto.
    /// </summary>
    /// <param name="chatId">ID del chat asociado.</param>
    /// <param name="messageId">ID del mensaje central.</param>
    /// <param name="before">Cantidad de mensajes previos a recuperar.</param>
    /// <param name="after">Cantidad de mensajes posteriores a recuperar.</param>
    /// <returns>
    /// Lista de mensajes incluyendo los anteriores y posteriores al mensaje solicitado.
    /// </returns>
    Task<List<ChatMessageModel>> GetMessagesWithContextAsync(
        string chatId,
        int messageId,
        int before,
        int after
    );

    /// <summary>
    /// Recupera todos los chats asociados a un mentor mediante su correo institucional.
    /// </summary>
    /// <param name="mentorEmail">Correo del mentor.</param>
    /// <returns>Lista de chats asociados al mentor.</returns>
    Task<List<Data.DB.EF.VirtualMentorDB.Entities.Chat>> GetChatsByMentorEmailAsync(string mentorEmail);

    /// <summary>
    /// Actualiza el identificador de Microsoft Teams (MS Teams Chat ID) correspondiente a un chat
    /// entre un mentor y un estudiante.
    /// </summary>
    /// <param name="mentorId">ID del mentor en la base de datos.</param>
    /// <param name="studentId">ID del estudiante en la base de datos.</param>
    /// <param name="chatId">ID del chat de Microsoft Teams.</param>
    Task UpdateMsTeamsChatIdAsync(int mentorId, int studentId, string chatId);

    /// <summary>
    /// Obtiene el chat activo entre un mentor y un estudiante, si existe.
    /// </summary>
    /// <param name="mentorId">ID del mentor.</param>
    /// <param name="studentId">ID del estudiante.</param>
    /// <returns>
    /// El chat correspondiente o null si no existe un registro activo.
    /// </returns>
    Task<Data.DB.EF.VirtualMentorDB.Entities.Chat?> GetChatByMentorAndStudentIdAsync(int mentorId, int studentId);

    /// <summary>
    /// Crea o actualiza un chat entre un estudiante y el mentor asignado según los datos de Excel.
    /// El proceso incluye desactivación de chats previos y activación del nuevo chat.
    /// </summary>
    /// <param name="studentId">ID del estudiante.</param>
    /// <param name="mentorEmail">Correo del mentor asignado.</param>
    Task CreateOrUpdateChatAsync(int studentId, string mentorEmail);

    /// <summary>
    /// Actualiza los datos generales de un chat existente.
    /// </summary>
    /// <param name="chat">Entidad del chat que será actualizada.</param>
    Task UpdateChatAsync(Data.DB.EF.VirtualMentorDB.Entities.Chat chat);

    /// <summary>
    /// Desactiva todos los chats asociados a un estudiante.
    /// </summary>
    /// <param name="studentId">ID del estudiante cuyos chats serán desactivados.</param>
    Task DeactivateChatsByStudentIdAsync(int studentId);

    /// <summary>
    /// Actualiza el estado de todos los chats asociados a un mentor.
    /// </summary>
    /// <param name="mentorId">Identificador del mentor.</param>
    /// <param name="state">Nuevo estado que se aplicará a los chats.</param>
    Task UpdateChatsByMentorIdAsync(int mentorId, string state);
}
