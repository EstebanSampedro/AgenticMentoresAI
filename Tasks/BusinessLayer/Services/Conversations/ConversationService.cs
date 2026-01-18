using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Conversations;

public class ConversationService : IConversationService
{
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly DBContext _context;

    public ConversationService(
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        DBContext context)
    {
        _serviceAccount = serviceAccountOptions.Value;
        _context = context;
    }

    public async Task<List<(int ConversationId, DateTimeOffset? LastStudentMessage)>>
    GetActiveConversationsWithLastStudentMessageAsync(DateTimeOffset threshold, CancellationToken ct)
    {
        var result = await _context.Conversations
            .Where(c => c.FinishedAt == null)
            .Select(c => new
            {
                ConversationId = c.Id,
                LastMsg = _context.Messages
                    .Where(m => m.ConversationId == c.Id && m.SenderRole == "Estudiante")
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => (DateTimeOffset?)m.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return result
            .Select(r => (r.ConversationId, r.LastMsg))
            .ToList();
    }

    public async Task FinalizeConversationsAsync(IEnumerable<int> ids, DateTimeOffset finished, CancellationToken ct)
    {
        var conversations = await _context.Conversations
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);

        foreach (var conversation in conversations)
        {
            var utcDate = finished.UtcDateTime;

            conversation.FinishedAt = utcDate;
            conversation.UpdatedAt = utcDate;
            conversation.UpdatedBy = _serviceAccount.Email;
        }

        await _context.SaveChangesAsync(ct);
    }
}
