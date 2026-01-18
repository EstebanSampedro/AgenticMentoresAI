using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Microsoft.EntityFrameworkCore;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;

/// <summary>
/// Servicio encargado de realizar la resolución de mentores a partir de un identificador
/// de chat de Microsoft Teams. Su función principal es determinar qué mentor es dueño
/// o está asociado a un chat en específico.
/// </summary>
public class MentorLookupService : IMentorLookupService
{
    private readonly DBContext _context;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de resolución de mentores.
    /// </summary>
    /// <param name="context">
    /// Contexto de base de datos utilizado para acceder a la relación entre chats y mentores.
    /// </param>
    public MentorLookupService(DBContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene el correo electrónico del mentor asociado a un chat de Microsoft Teams.
    /// </summary>
    /// <param name="chatId">
    /// Identificador del chat en Teams (<c>MsteamsChatId</c>) que se desea resolver.
    /// </param>
    /// <returns>
    /// El correo electrónico del mentor si existe una relación válida en la base de datos,
    /// o <c>null</c> si no se encuentra ningún mentor asociado o si el chat no existe.
    /// </returns>
    /// <remarks>
    /// Este método asume que cada chat registrado en la base de datos tiene una relación
    /// uno-a-uno con un mentor. Solo devuelve el primer resultado encontrado, en caso
    /// de existir inconsistencias de datos.
    /// </remarks>
    public async Task<string?> GetMentorEmailByChatIdAsync(string chatId)
    {
        // Realiza la búsqueda del chat por su MSTeamsChatId,
        // luego proyecta directamente el correo del mentor asociado.
        // FirstOrDefaultAsync devuelve null si no existe coincidencia.
        return await _context.Chats
            .Where(c => c.MsteamsChatId == chatId)
            .Select(c => c.Mentor.Email)
            .FirstOrDefaultAsync();
    }
}
