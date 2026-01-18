using Newtonsoft.Json;
using System.Globalization;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public record SalesforceTerm
{
    [JsonProperty("Name")]
    public string Name { get; init; } = default!;

    [JsonProperty("Codigo_banner__c")]
    public string? BannerCode { get; init; }

    [JsonProperty("Linea_de_negocio__c")]
    public string? BusinessLine { get; init; }

    // Salesforce (Date) devuelve "yyyy-MM-dd"
    [JsonProperty("hed__Start_Date__c")]
    public string? StartDateRaw { get; init; }

    [JsonProperty("hed__End_Date__c")]
    public string? EndDateRaw { get; init; }

    public DateOnly? StartDate =>
        DateOnly.TryParseExact(StartDateRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;

    public DateOnly? EndDate =>
        DateOnly.TryParseExact(EndDateRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
}
