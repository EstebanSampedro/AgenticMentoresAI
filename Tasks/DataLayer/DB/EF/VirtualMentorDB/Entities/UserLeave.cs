using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class UserLeave
{
    public int Id { get; set; }

    public int MentorId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string UserLeaveState { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string UpdatedBy { get; set; } = null!;

    public virtual UserTable Mentor { get; set; } = null!;
}
