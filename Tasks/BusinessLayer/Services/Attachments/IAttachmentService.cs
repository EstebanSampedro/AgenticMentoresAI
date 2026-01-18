using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Attachments;

public interface IAttachmentService
{
    Task<List<string>> SaveAttachmentsAndBuildInternalUrlsAsync(
        ChatMessage message,
        int messageDbId,
        string chatId
    );
}
