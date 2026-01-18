using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Services;

public interface IMessageService
{
    Task<int> SaveMessageAsync(
        string chatId,
        string messageId,
        string senderEntraUserId,
        string content,
        string contentType);

    Task<Message?> GetMessageWithChatAndAttachmentsAsync(string chatId, string messageId);

    Task<NewMessageModel?> GetMessageResponseAsync(Message messageEntity);

    Task<(string CleanText, List<AttachmentModel> InlineAttachments)>
        ProcessInlineImagesAsync(string contentHtml, List<string> extractedUrls);

    Task<List<AttachmentModel>>
        MapDbAttachmentsAsync(ICollection<MessageAttachment>? attachments);

    Task<List<string>> AddAttachmentsAsync(
        int messageDbId,
        List<MessageAttachment> attachments);
}
