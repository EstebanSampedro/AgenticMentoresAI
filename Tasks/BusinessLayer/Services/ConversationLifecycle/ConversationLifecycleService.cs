using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Conversations;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.ConversationLifecycle;

public class ConversationLifecycleService : IConversationLifecycleService
{
    private readonly IConversationService _conversationService;
    private readonly ConversationTimeoutOptions _conversationTimeoutOptions;

    public ConversationLifecycleService(
        IConversationService conversationService,
        IOptions<ConversationTimeoutOptions> conversationTimeoutOptions)
    {
        _conversationService = conversationService;
        _conversationTimeoutOptions = conversationTimeoutOptions.Value;        
    }

    public async Task<int> FinalizeInactiveConversationsAsync(CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;
        var threshold = now.AddMinutes(_conversationTimeoutOptions.StudentInactivityMinutes);

        var active = await _conversationService
        .GetActiveConversationsWithLastStudentMessageAsync(threshold, stoppingToken);

        int finalizedCount = 0;

        var toFinalize = active
            .Where(x => x.LastStudentMessage == null || x.LastStudentMessage <= threshold)
            .Select(x => x.ConversationId)
            .ToList();

        if (!toFinalize.Any()) return 0;

        await _conversationService.FinalizeConversationsAsync(toFinalize, now, stoppingToken);

        return finalizedCount;
    }
}
