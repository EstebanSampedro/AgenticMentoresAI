using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class AiPendingBatch
{
    public int Id { get; set; }

    public string ChatId { get; set; } = null!;

    public DateTime WindowEndsAt { get; set; }

    public string Status { get; set; } = null!;

    public string? AccumulatedText { get; set; }

    public string? AccumulatedImages { get; set; }

    public string? LastMessageId { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public byte[] RowVersion { get; set; } = null!;
}
