using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.MicrosoftGraph;

public interface IGraphService
{
    Task<ChatMessage?> GetChatMessageAsync(string chatId, string messageId);
    Task<bool> IsOneToOneChatAsync(string chatId);
    Task<List<AadUserConversationMember>> GetMembersFromChatAsync(string chatId);

    Task<string?> GetOtherUserFromChatAsync(string chatId, string senderEntraId);
    Task<byte[]?> GetImageContentFromHostedContentAsync(string chatId, string messageId, string hostedContentId);
    Task<(string? driveId, string? itemId, string? name, string? mime)> ResolveDriveItemAsync(string contentUrl);
}
