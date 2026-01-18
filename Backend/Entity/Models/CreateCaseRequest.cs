using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class CreateCaseRequest
{
    public string Status { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "";

    // Campos personalizados (custom fields)
    [JsonProperty("Estado_seguimiento__c")]
    public string? TrackingStatus { get; set; }

    [JsonProperty("Estado_contacto__c")]
    public string? ContactStatus { get; set; }

    [JsonProperty("Via_contacto__c")]
    public string? WayContact { get; set; }

    [JsonProperty("Tipo_contacto__c")]
    public string? TypeContact { get; set; }

    [JsonProperty("Persona_contactada__c")]
    public string? PersonContacted { get; set; }

    [JsonProperty("Resultado_atencion_1__c")]
    public string? AttentionResult1 { get; set; }

    [JsonProperty("Resultado_atencion_2__c")]
    public string? AttentionResult2 { get; set; }

    [JsonProperty("Fecha_proximo_caso__c")]
    public string? NextDate { get; set; } // formato yyyy-MM-dd

    [JsonProperty("Contact")]
    public BannerCodeRequest? Contact { get; set; }

    // Persona responsable por BannerCode (NO UserId)
    [JsonProperty("Persona_responsable__r")]
    public BannerCodeRequest? ResponsiblePerson { get; set; }

    // RecordType por Name (NO Id)
    [JsonProperty("RecordType")]
    public RecordTypeRequest? RecordType { get; set; }

    // Owner (User o Queue)
    //[JsonProperty("Owner")]
    //public OwnerRequest? Owner { get; set; }

    //[JsonProperty("Universidad_interes__c")]
    //public string? UniversidadInteres { get; set; }
}
