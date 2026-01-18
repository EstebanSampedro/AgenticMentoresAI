using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class UserTable
{
    public int Id { get; set; }

    public string? EntraUserId { get; set; }

    public string? BannerId { get; set; }

    public string? Pidm { get; set; }

    public string Email { get; set; } = null!;

    public string? BackupEmail { get; set; }

    public string FullName { get; set; } = null!;

    public string? FavoriteName { get; set; }

    public string UserRole { get; set; } = null!;

    public string UserType { get; set; } = null!;

    public string UserState { get; set; } = null!;

    public string? SpecialConsideration { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public string UpdatedBy { get; set; } = null!;

    public string? Identification { get; set; }

    public string? Career { get; set; }

    public string? Faculty { get; set; }

    public int? CurrentSemester { get; set; }

    public string? Gender { get; set; }

    public virtual ICollection<Chat> ChatMentors { get; set; } = new List<Chat>();

    public virtual ICollection<Chat> ChatStudents { get; set; } = new List<Chat>();

    public virtual ICollection<StudentContextInfo> StudentContextInfos { get; set; } = new List<StudentContextInfo>();

    public virtual ICollection<UserLeave> UserLeaves { get; set; } = new List<UserLeave>();
}
