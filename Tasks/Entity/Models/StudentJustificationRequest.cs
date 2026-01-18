using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class StudentJustificationRequest
{
    public string BannerId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Comment { get; set; } = default!;
}
