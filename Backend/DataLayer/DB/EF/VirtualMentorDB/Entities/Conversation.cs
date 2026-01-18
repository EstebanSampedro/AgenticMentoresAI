using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;

public partial class Conversation
{
    public int Id { get; set; }

    public int ChatId { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime? UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public virtual Chat Chat { get; set; } = null!;

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
