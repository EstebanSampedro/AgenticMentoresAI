using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class Summary
{
    public int Id { get; set; }

    public int ChatId { get; set; }

    public string Summary1 { get; set; } = null!;

    public string SummaryType { get; set; } = null!;

    public string? KeyPoints { get; set; }

    public string? EscalationReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string? Theme { get; set; }

    public string? Priority { get; set; }

    public virtual Chat Chat { get; set; } = null!;
}
