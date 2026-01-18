using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public sealed class ImageAnalysisResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("data")] public ImageAnalysisData? Data { get; set; }

    // Pueden ser null u objetos/arrays: usa JsonElement? para ser flexible
    [JsonPropertyName("errors")] public JsonElement? Errors { get; set; }
    [JsonPropertyName("meta")] public JsonElement? Meta { get; set; }
}

public sealed class ImageAnalysisData
{
    [JsonPropertyName("analysis")] public string? Analysis { get; set; }
    [JsonPropertyName("summary")] public string? Summary { get; set; }
    [JsonPropertyName("certificate")] public string? Certificate { get; set; }
    [JsonPropertyName("escalated")] public string? Escalated { get; set; }
    [JsonPropertyName("fullName")] public string? FullName { get; set; }
    [JsonPropertyName("dateInit")] public string? DateInit { get; set; }
    [JsonPropertyName("dateEnd")] public string? DateEnd { get; set; }
    [JsonPropertyName("identification")] public string? Identification { get; set; }
}
