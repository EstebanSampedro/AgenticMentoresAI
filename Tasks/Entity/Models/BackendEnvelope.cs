using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public sealed class BackendEnvelope<T>
{
    public int ResponseCode { get; set; }
    public string? ResponseMessage { get; set; }
    public T? ResponseData { get; set; }
}

public sealed class GeneralSalesforceUserResponse
{
    public int TotalSize { get; set; }
    public bool Done { get; set; }
    public List<GeneralSalesforceUserRecord> Records { get; set; } = new();
}

public sealed class GeneralSalesforceUserRecord
{
    public string? Id { get; set; }
    public GeneralSalesforceAttributes? Attributes { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Hed__UniversityEmail__c { get; set; }
    public string? Codigo_banner__c { get; set; }
    public string? Asignacion__c { get; set; }
    public int? Estudiantes_Asignados_Actualmente_Mentor__c { get; set; }
    public int? Limite_Asignado__c { get; set; }
}

public sealed class GeneralSalesforceAttributes
{
    public string? Type { get; set; }
    public string? Url { get; set; }
}
