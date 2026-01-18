using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class StudentContextInfo
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string Identification { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Nickname { get; set; }

    public string? Career { get; set; }

    public string? Faculty { get; set; }

    public int? CurrentSemester { get; set; }

    public virtual UserTable Student { get; set; } = null!;
}
