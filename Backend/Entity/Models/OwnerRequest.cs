using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public class OwnerRequest
{
    [JsonProperty("attributes")]
    public AttributeRequest Attributes { get; set; }

    // Para User
    public string? Email { get; set; }
}

public class AttributeRequest
{
    [JsonProperty("type")]
    public string Type { get; set; }

    public AttributeRequest(string type)
    {
        Type = type; // "User" o "Group"
    }
}

