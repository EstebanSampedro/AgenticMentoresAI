using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class MessageAttachment
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public string ContentUrl { get; set; } = null!;

    public string? FileName { get; set; }

    public string? ContentType { get; set; }

    public string SourceType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public string InternalContentUrl { get; set; } = null!;

    public string? DriveId { get; set; }

    public string? ItemId { get; set; }

    public string? MimeType { get; set; }

    public virtual Message Message { get; set; } = null!;
}
