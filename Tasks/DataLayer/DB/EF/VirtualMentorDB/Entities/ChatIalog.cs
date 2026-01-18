using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class ChatIalog
{
    public int Id { get; set; }

    public int ChatId { get; set; }

    public bool Iastate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string UpdatedBy { get; set; } = null!;

    public string IachangeReason { get; set; } = null!;

    public virtual Chat Chat { get; set; } = null!;
}
