using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class CreateJustificationRequest
{
    public string ChatId { get; set; } = default!;
    public string StudentEmail { get; set; } = default!;
    public string CertificateType { get; set; } = default!;
    public string? FullName { get; set; }
    public string? Identification { get; set; }
    public string? DateInit { get; set; }
    public string? DateEnd { get; set; }
    public string? EvidenceImageUrl { get; set; }
    public string? Analysis { get; set; }
    public string? Summary { get; set; }
    public string? Source { get; set; }
}
