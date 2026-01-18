using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;

public partial class DataProtectionKey
{
    public int Id { get; set; }

    public string? FriendlyName { get; set; }

    public string? Xml { get; set; }
}
