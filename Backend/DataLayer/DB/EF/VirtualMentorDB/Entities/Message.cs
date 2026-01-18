using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;

public partial class Message
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public string MsteamsMessageId { get; set; } = null!;

    public string SenderRole { get; set; } = null!;

    public string MessageContent { get; set; } = null!;

    public string MessageContentType { get; set; } = null!;

    public string MessageStatus { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual ICollection<MessageAttachment> MessageAttachments { get; set; } = new List<MessageAttachment>();
}
