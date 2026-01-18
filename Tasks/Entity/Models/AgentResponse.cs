using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public sealed class AgentResponseData
{
    [JsonPropertyName("session_id")] public string? SessionId { get; set; }
    [JsonPropertyName("prompt")] public string? Prompt { get; set; }
    [JsonPropertyName("nombre_completo")] public string? NombreCompleto { get; set; }
    [JsonPropertyName("apodo")] public string? Apodo { get; set; }
    [JsonPropertyName("cedula")] public string? Cedula { get; set; }
    [JsonPropertyName("carrera")] public string? Carrera { get; set; }
    [JsonPropertyName("correo")] public string? Correo { get; set; }
    [JsonPropertyName("response")] public string? Response { get; set; }
    [JsonPropertyName("timestamp")] public DateTimeOffset? Timestamp { get; set; }

    // Compatibilidad hacia atrás (si algún día vuelven a enviar esto):
    [JsonPropertyName("analysis")] public string? Analysis { get; set; }
    [JsonPropertyName("summary")] public string? Summary { get; set; }
    [JsonPropertyName("certificate")] public string? Certificate { get; set; }

    // Captura campos desconocidos sin romper el parseo
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class AgentResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public AgentResponseData? Data { get; set; }

    // Si quieres preservar el payload tal cual:
    [JsonPropertyName("errors")] public JsonElement? Errors { get; set; }
    [JsonPropertyName("meta")] public JsonElement? Meta { get; set; }
}
