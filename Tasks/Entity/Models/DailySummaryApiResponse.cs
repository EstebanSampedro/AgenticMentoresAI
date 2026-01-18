using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

public sealed class DailySummaryApiResponse
{
    [JsonPropertyName("ResponseCode")] public int Code { get; set; }
    [JsonPropertyName("ResponseMessage")] public string? Message { get; set; }
    [JsonPropertyName("ResponseData")] public SummaryData? Data { get; set; }
}

public sealed class SummaryData
{
    [JsonPropertyName("ChatId")] public string ChatId { get; set; } = "";
    [JsonPropertyName("Summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("SummaryType")] public string SummaryType { get; set; } = "";

    [JsonPropertyName("KeyPoints")] public string KeyPointsRaw { get; set; } = "";

    [JsonPropertyName("Escalated")] public string Escalated { get; set; } = "";

    [JsonPropertyName("EscalationReason")] public string EscalationReason { get; set; } = "";
    [JsonPropertyName("Theme")] public string Theme { get; set; } = "";
    [JsonPropertyName("Priority")] public string Priority { get; set; } = "";
    [JsonPropertyName("CreatedAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("CreatedBy")] public string CreatedBy { get; set; } = "";

    // Método helper para obtener lista limpia de puntos clave
    public List<string> GetKeyPoints() =>
        KeyPointsRaw.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}

