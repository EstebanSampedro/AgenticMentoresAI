using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class BasicInformationResponse
{
    [JsonPropertyName("responseCode")]
    public int ResponseCode { get; set; }

    [JsonPropertyName("responseMessage")]
    public string ResponseMessage { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public ContentData? Content { get; set; }
}

public class ContentData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("personId")]
    public string PersonId { get; set; } = string.Empty;

    [JsonPropertyName("bannerId")]
    public string BannerId { get; set; } = string.Empty;

    [JsonPropertyName("pidm")]
    public string Pidm { get; set; } = string.Empty;

    [JsonPropertyName("personalEmail")]
    public string PersonalEmail { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumbers")]
    public PhoneNumbers? PhoneNumbers { get; set; }

    [JsonPropertyName("student")]
    public StudentData? Student { get; set; }

    [JsonPropertyName("teacher")]
    public object? Teacher { get; set; }
}

public class PhoneNumbers
{
    [JsonPropertyName("house")]
    public string House { get; set; } = string.Empty;

    [JsonPropertyName("cellphone1")]
    public string Cellphone1 { get; set; } = string.Empty;

    [JsonPropertyName("cellphone2")]
    public string Cellphone2 { get; set; } = string.Empty;

    [JsonPropertyName("altern")]
    public string Altern { get; set; } = string.Empty;

    [JsonPropertyName("office")]
    public string Office { get; set; } = string.Empty;

    [JsonPropertyName("parents")]
    public string Parents { get; set; } = string.Empty;

    [JsonPropertyName("emergency")]
    public string Emergency { get; set; } = string.Empty;
}

public class StudentData
{
    [JsonPropertyName("pidm")]
    public string Pidm { get; set; } = string.Empty;

    [JsonPropertyName("bannerId")]
    public string BannerId { get; set; } = string.Empty;

    [JsonPropertyName("institutionalEmail")]
    public string InstitutionalEmail { get; set; } = string.Empty;

    [JsonPropertyName("pgaGlobal")]
    public string PgaGlobal { get; set; } = string.Empty;

    [JsonPropertyName("studentCarrers")]
    public List<StudentCareer> StudentCareers { get; set; } = new();
}

public class StudentCareer
{
    [JsonPropertyName("programCode")]
    public string ProgramCode { get; set; } = string.Empty;

    [JsonPropertyName("programDesc")]
    public string ProgramDesc { get; set; } = string.Empty;

    [JsonPropertyName("programType")]
    public string ProgramType { get; set; } = string.Empty;
}
