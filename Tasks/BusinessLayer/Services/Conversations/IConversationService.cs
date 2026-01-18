namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Conversations;

public interface IConversationService
{
    Task<List<(int ConversationId, DateTimeOffset? LastStudentMessage)>>
    GetActiveConversationsWithLastStudentMessageAsync(DateTimeOffset threshold, CancellationToken ct);

    Task FinalizeConversationsAsync(IEnumerable<int> conversationIds, DateTimeOffset finishedAt, CancellationToken ct);
}
