namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;

/// <summary>
/// Define las operaciones necesarias para resolver información de mentores
/// a partir de identificadores de chats de Microsoft Teams.
/// </summary>
public interface IMentorLookupService
{
    /// <summary>
    /// Obtiene el correo electrónico del mentor asociado a un chat de Microsoft Teams.
    /// </summary>
    /// <param name="chatId">
    /// Identificador del chat en Teams (<c>MsteamsChatId</c>) utilizado para buscar
    /// el mentor correspondiente.
    /// </param>
    /// <returns>
    /// Una cadena con el correo electrónico del mentor si existe una asociación válida en la base de datos,
    /// o <c>null</c> si el chat no existe o no tiene un mentor relacionado.
    /// </returns>
    /// <remarks>
    /// Este método se utiliza generalmente en procesos donde es necesario obtener el contexto
    /// del usuario dueño del chat (por ejemplo, para recuperación de tokens o procesamiento de contenido).
    /// </remarks>
    Task<string?> GetMentorEmailByChatIdAsync(string chatId);
}
