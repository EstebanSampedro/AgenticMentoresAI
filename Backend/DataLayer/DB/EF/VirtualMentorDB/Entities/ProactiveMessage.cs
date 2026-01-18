using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;

public partial class ProactiveMessage
{
    public int Id { get; set; }

    public string MessageKey { get; set; } = null!;

    public string MessageContent { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime? DeletedAt { get; set; }

    public string DeletedBy { get; set; } = null!;
}
