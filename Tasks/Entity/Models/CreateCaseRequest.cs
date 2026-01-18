using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public sealed record CreateCaseRequest(
    string BannerStudent,
    string BannerMentor,
    string OwnerEmail,
    string Summary,
    string Theme,
    DateOnly NextDate
);
