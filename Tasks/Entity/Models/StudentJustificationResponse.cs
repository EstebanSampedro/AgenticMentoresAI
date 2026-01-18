using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public class StudentJustificationResponse
{
    [JsonPropertyName("responseCode")]
    public int ResponseCode { get; set; }

    [JsonPropertyName("responseMessage")]
    public string? ResponseMessage { get; set; }

    [JsonPropertyName("content")]
    public bool Content { get; set; }
}
