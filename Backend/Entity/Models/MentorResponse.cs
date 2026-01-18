namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class MentorResponse
{
    public string Id { get; set; }
    public AttributeResponse Attributes { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string hed__UniversityEmail__c { get; set; }
    public string Codigo_banner__c { get; set; }
    public string Asignacion__c { get; set; }
    public double? Estudiantes_Asignados_Actualmente_Mentor__c { get; set; }
    public double? Limite_Asignado__c { get; set; }
}

public class AttributeResponse
{
    public string Type { get; set; }
    public string Url { get; set; }
}
