using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class RecordTypeRequest
{
    public string Name { get; set; }

    public RecordTypeRequest(string name)
    {
        Name = name;
    }
}
