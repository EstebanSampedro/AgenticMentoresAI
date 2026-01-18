using Academikus.AgenteInteligenteMentoresTareas.Business.Services.MicrosoftGraph;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Academikus.AgenteInteligenteMentoresTareas.Entity.OutPut;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Chat;

public class ChatService : IChatService
{
    private readonly DBContext _context;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly IGraphService _graphService;

    public ChatService(
        IGraphService graphService,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        DBContext context)
    {
        _graphService = graphService;
        _serviceAccount = serviceAccountOptions.Value;
        _context = context;
    }

    /// <summary>
    /// Verifica si un chat existe en la base de datos por su MSTeamsChatId
    /// y si el usuario emisor pertenece a ese chat.
    /// </summary>
    /// <param name="msTeamsChatId">Identificador del chat en Microsoft Teams.</param>
    /// <param name="senderEntraUserId">Identificador del usuario que envió el mensaje.</param>
    /// <returns>
    /// True si el chat existe en BD y el usuario pertenece.
    /// False si el chat no existe o el usuario no está asociado.
    /// </returns>
    public async Task<bool> ChatExistsForUserAsync(string msTeamsChatId, string senderEntraUserId)
    {
        // Validaciones tempranas de parámetros
        if (string.IsNullOrWhiteSpace(msTeamsChatId) || string.IsNullOrWhiteSpace(senderEntraUserId))
            return false;

        // Buscar el chat en BD único por MSTeamsChatId
        var chat = await _context.Chats
            .Include(c => c.Mentor)
            .Include(c => c.Student)
            .FirstOrDefaultAsync(c => c.MsteamsChatId == msTeamsChatId);

        if (chat == null)
            return false;

        // Determinar si el usuario corresponde al mentor o estudiante del chat
        bool userBelongsToChat =
            chat.Mentor?.EntraUserId == senderEntraUserId ||
            chat.Student?.EntraUserId == senderEntraUserId;

        return userBelongsToChat;
    }

    public async Task<ChatIdAssignmentResultDto> UpdateChatMsTeamsIdAsync(
    string senderEntraId,
    string msTeamsChatId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(senderEntraId) || string.IsNullOrWhiteSpace(msTeamsChatId))
            {
                Console.WriteLine("[ChatUpdate] senderEntraId o msTeamsChatId inválidos.");
                return null;
            }

            // Obtiene el remitente
            var sender = await _context.UserTables
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.EntraUserId == senderEntraId);

            if (sender == null)
            {
                Console.WriteLine("[ChatUpdate] No se encontró el usuario remitente");
                return null;
            }

            // Obtiene el otro usuario del chat desde Graph
            var otherUserEntraId = await _graphService.GetOtherUserFromChatAsync(msTeamsChatId, senderEntraId);

            if (otherUserEntraId == null)
            {
                Console.WriteLine("[ChatUpdate] No se pudo obtener el otro usuario del chat");
                return null;
            }

            // Obtiene ID interno del otro usuario
            var other = await _context.UserTables
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.EntraUserId == otherUserEntraId);

            if (other == null)
            {
                Console.WriteLine("[ChatUpdate] No se encontró el otro usuario en UserTable");
                return null;
            }

            // Busca el chat exacto entre ambos usuarios
            var chat = await _context.Chats
                .FirstOrDefaultAsync(c =>
                    c.MsteamsChatId == null &&
                    (
                        (c.MentorId == sender.Id && c.StudentId == other.Id) ||
                        (c.MentorId == other.Id && c.StudentId == sender.Id)
                    )
                );

            if (chat == null)
            {
                Console.WriteLine("[ChatUpdate] No existe un chat entre estos dos usuarios con MSTeamsChatId");
                return null;
            }

            // Determinar StudentDbId y MentorDbId desde el registro Chat
            var studentDbId = chat.StudentId;
            var mentorDbId = chat.MentorId;

            // Obtener EntraId del mentor (para SignalR group mentor:{entraId})
            var mentorEntraId = await _context.UserTables
                .AsNoTracking()
                .Where(u => u.Id == mentorDbId)
                .Select(u => u.EntraUserId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(mentorEntraId))
            {
                Console.WriteLine("[ChatUpdate] No se pudo obtener el EntraUserId del mentor.");
                return null;
            }

            // Actualizar
            chat.MsteamsChatId = msTeamsChatId;
            chat.UpdatedAt = DateTime.UtcNow;
            chat.Iaenabled = true;
            chat.UpdatedBy = _serviceAccount.Email;

            await _context.SaveChangesAsync();

            return new ChatIdAssignmentResultDto
            {
                ChatDbId = chat.Id,
                StudentDbId = studentDbId,
                MentorEntraId = mentorEntraId,
                MsTeamsChatId = msTeamsChatId
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatUpdate] Error: {ex.Message}");
            return null;
        }
    }


    public async Task MarkChatAsUnreadIfStudentAsync(Message messageEntity)
    {
        if (messageEntity?.SenderRole != "Estudiante")
            return;

        var chat = messageEntity?.Conversation?.Chat;
        if (chat == null)
            return;

        chat.IsRead = false;
        chat.UpdatedAt = DateTime.UtcNow;
        chat.UpdatedBy = _serviceAccount.Email;

        await _context.SaveChangesAsync();
    }

    public async Task<bool?> GetAiEnabledStateAsync(string msTeamsChatId)
    {
        return await _context.Chats
            .Where(c => c.MsteamsChatId == msTeamsChatId)
            .Select(c => c.Iaenabled)
            .FirstOrDefaultAsync();
    }

    public async Task<(string? MentorBannerId, string? StudentBannerId, string? MentorEmail)?>
        GetBannerIdsForChatAsync(string msTeamsChatId)
    {
        var result = await (
            from ch in _context.Chats.AsNoTracking()
            where ch.MsteamsChatId == msTeamsChatId
            join m in _context.UserTables.AsNoTracking() on ch.MentorId equals m.Id
            join s in _context.UserTables.AsNoTracking() on ch.StudentId equals s.Id
            select new
            {
                MentorBannerId = m.BannerId,
                StudentBannerId = s.BannerId,
                MentorEmail = m.Email
            }
        ).FirstOrDefaultAsync();

        if (result == null)
            return null;

        return (result.MentorBannerId, result.StudentBannerId, result.MentorEmail);
    }

    public async Task<List<string>> GetChatsWithMessagesInWindowAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct)
    {
        var chatIds = await (
            from msg in _context.Messages.AsNoTracking()
            where msg.CreatedAt >= startUtc && msg.CreatedAt < endUtc
            select msg.Conversation!.Chat!.MsteamsChatId!
        ).Distinct().ToListAsync(ct);
        return chatIds;
    }

    public async Task<int?> GetChatIdAsync(string msTeamsChatId)
    {
        return await _context.Chats.AsNoTracking()
            .Where(c => c.MsteamsChatId == msTeamsChatId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsAiEnabledForChatAsync(string chatId)
    {
        return await _context.Chats
            .Where(c => c.MsteamsChatId == chatId)
            .Select(c => c.Iaenabled)
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetStudentEmailByChatIdAsync(string chatId)
    {
        return await _context.UserTables
            .Where(u => u.UserRole == "Estudiante" &&
                        _context.Chats.Any(c => c.MsteamsChatId == chatId &&
                                              (c.StudentId == u.Id || c.MentorId == u.Id)))
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateLastAiBatchDateAsync(string chatId)
    {
        var chat = await _context.Chats.FirstOrDefaultAsync(c => c.MsteamsChatId == chatId);
        if (chat != null)
        {
            chat.LastAiBatchAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
