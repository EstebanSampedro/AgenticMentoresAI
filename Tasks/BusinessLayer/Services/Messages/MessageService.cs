using Academikus.AgenteInteligenteMentoresTareas.Business.Common;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Users;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Academikus.AgenteInteligenteMentoresTareas.Utility.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Services;

public class MessageService : IMessageService
{
    private readonly DBContext _context;
    private readonly ServiceAccountOptions _serviceAccount;
    private readonly IUserService _userService;

    public MessageService(
        DBContext context,
        IOptions<ServiceAccountOptions> serviceAccountOptions,
        IUserService userService)
    {
        _context = context;
        _serviceAccount = serviceAccountOptions.Value;
        _userService = userService;
    }

    public async Task<int> SaveMessageAsync(string msTeamsChatId, string msTeamsMessageId, string senderEntraUserId, string content, string contentType)
    {
        var chat = await _context.Chats
            .FirstOrDefaultAsync(c => c.MsteamsChatId == msTeamsChatId);

        if (chat == null)
        {
            Console.WriteLine($"No se encontró el chat | ChatId={msTeamsChatId}");
            return -1;
        }

        string senderRole = await _userService.GetUserRoleAsync(chat, senderEntraUserId);

        var activeConversation = await _context.Conversations
            .Where(c => c.ChatId == chat.Id && c.FinishedAt == null)
            .OrderByDescending(c => c.StartedAt)
            .FirstOrDefaultAsync();

        if (activeConversation == null)
        {
            activeConversation = new Data.DB.EF.VirtualMentorDB.Entities.Conversation
            {
                ChatId = chat.Id,
                StartedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _serviceAccount.Email,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = _serviceAccount.Email
            };

            _context.Conversations.Add(activeConversation);
            await _context.SaveChangesAsync();
        }

        var newMessage = new Data.DB.EF.VirtualMentorDB.Entities.Message
        {
            ConversationId = activeConversation.Id,
            MsteamsMessageId = msTeamsMessageId,
            SenderRole = senderRole,
            MessageContent = content,
            MessageContentType = contentType,
            MessageStatus = "Creado",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _serviceAccount.Email,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = _serviceAccount.Email
        };

        _context.Messages.Add(newMessage);
        await _context.SaveChangesAsync();

        Console.WriteLine($"Mensaje agregado | ConversationId={activeConversation.Id}");

        return newMessage.Id;
    }

    public async Task<Message?> GetMessageWithChatAndAttachmentsAsync(string chatId, string messageId)
    {
        return await _context.Messages
            .Include(m => m.MessageAttachments)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.Chat)
            .FirstOrDefaultAsync(m =>
                m.MsteamsMessageId == messageId &&
                m.Conversation.Chat.MsteamsChatId == chatId);
    }

    public async Task<NewMessageModel?> GetMessageResponseAsync(Message messageEntity)
    {
        if (messageEntity == null)
            return null;

        return new NewMessageModel
        {
            ChatId = messageEntity.Conversation.Chat.MsteamsChatId,
            Content = messageEntity.MessageContent,
            MessageId = messageEntity.MsteamsMessageId,
            SenderRole = messageEntity.SenderRole,
            ContentType = messageEntity.MessageContentType,
            Timestamp = messageEntity.CreatedAt?
                        .ToString("yyyy-MM-ddTHH:mm:ssZ") ?? string.Empty,
            IAEnabled = messageEntity.Conversation.Chat.Iaenabled
        };
    }

    public async Task<(string CleanText, List<AttachmentModel> InlineAttachments)>
        ProcessInlineImagesAsync(string contentHtml, List<string> extractedUrls)
    {
        var cleaned = HtmlUtils.ProcessHtmlAndStripImages(contentHtml);

        // Combinar imágenes encontradas en el HTML + adjuntos guardados en DB
        cleaned.Urls.AddRange(extractedUrls ?? new List<string>());

        var inlineAttachments = cleaned.Urls
            .Distinct()
            .Select(url => new AttachmentModel
            {
                FileName = "",
                FileType = "image",
                ContentType = "image/*",
                DownloadUrl = url,
                SourceType = "hostedContent"
            })
            .ToList();

        return (cleaned.CleanText, inlineAttachments);
    }

    public Task<List<AttachmentModel>> 
        MapDbAttachmentsAsync(ICollection<MessageAttachment>? attachments)
    {
        // Si no hay adjuntos → lista vacía
        if (attachments == null || attachments.Count == 0)
            return Task.FromResult(new List<AttachmentModel>());

        var mapped = attachments.Select(a => new AttachmentModel
        {
            FileName = a.FileName ?? string.Empty,
            FileType = GetFileTypeCategory(a.MimeType, a.FileName),
            ContentType = string.IsNullOrWhiteSpace(a.ContentType)
                            ? "application/octet-stream"
                            : a.ContentType,
            DownloadUrl =
                    a.SourceType == "inline-image"
                        ? (a.InternalContentUrl ?? "")
                        : (a.ContentUrl ?? ""),
            SourceType = a.SourceType
        })
        .ToList();

        return Task.FromResult(mapped);
    }

    public async Task<List<string>> AddAttachmentsAsync(
    int messageDbId,
    List<MessageAttachment> attachments)
    {
        if (attachments == null || attachments.Count == 0)
            return new List<string>();

        _context.MessageAttachments.AddRange(attachments);
        await _context.SaveChangesAsync();

        var baseUrl = EnvironmentHelper.GetBackendBaseUrl();
        var urls = new List<string>();

        foreach (var a in attachments)
        {
            a.InternalContentUrl = $"{baseUrl}/api/attachments/{a.DriveId}/{a.ItemId}";
            a.UpdatedAt = DateTime.UtcNow;

            urls.Add(a.InternalContentUrl);
        }

        _context.MessageAttachments.UpdateRange(attachments);
        await _context.SaveChangesAsync();

        return urls;
    }

    private static string GetFileTypeCategory(string? contentType, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var ct = contentType.ToLowerInvariant();
            if (ct.StartsWith("image/")) return "image";
            if (ct == "application/pdf") return "pdf";
            if (ct.StartsWith("audio/")) return "audio";
            if (ct.StartsWith("video/")) return "video";
            if (ct.Contains("word") || ct.Contains("msword") || ct.Contains("officedocument.wordprocessingml")) return "doc";
            if (ct.Contains("excel") || ct.Contains("spreadsheetml")) return "xls";
            if (ct.Contains("powerpoint") || ct.Contains("presentationml")) return "ppt";
            if (ct.Contains("zip") || ct.Contains("compressed")) return "zip";
        }

        // Fallback por extensión si no hay MIME confiable
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tif" or ".tiff" or ".heic" => "image",
                ".pdf" => "pdf",
                ".doc" or ".docx" => "doc",
                ".xls" or ".xlsx" => "xls",
                ".ppt" or ".pptx" => "ppt",
                ".zip" or ".rar" or ".7z" => "zip",
                ".mp3" or ".wav" or ".ogg" => "audio",
                ".mp4" or ".mov" or ".avi" or ".mkv" => "video",
                _ => "file"
            };
        }

        return "file";
    }
}
