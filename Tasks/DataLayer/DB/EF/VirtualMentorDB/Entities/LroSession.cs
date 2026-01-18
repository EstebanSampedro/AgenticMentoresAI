using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;

public partial class LroSession
{
    public int Id { get; set; }

    public string UserObjectId { get; set; } = null!;

    public byte[] SessionKey { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTimeOffset? LastUsedAt { get; set; }

    public string LastUsedBy { get; set; } = null!;

    public string UserUpn { get; set; } = null!;
}
