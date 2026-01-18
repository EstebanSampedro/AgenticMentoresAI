using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class Chat
{
    public int Id { get; set; }

    public int MentorId { get; set; }

    public int StudentId { get; set; }

    public string? MsteamsChatId { get; set; }

    public bool Iaenabled { get; set; }

    public string ChatState { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string UpdatedBy { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime? LastAiBatchAt { get; set; }

    public virtual ICollection<ChatIalog> ChatIalogs { get; set; } = new List<ChatIalog>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual UserTable Mentor { get; set; } = null!;

    public virtual UserTable Student { get; set; } = null!;

    public virtual ICollection<Summary> Summaries { get; set; } = new List<Summary>();
}
