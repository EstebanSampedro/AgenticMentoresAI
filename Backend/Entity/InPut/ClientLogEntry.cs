using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;

public record ClientLogEntry
{
    [Required, MinLength(1)]
    public string Message { get; init; } = default!;

    public string? UserId { get; init; }
    public string? ChatId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClientLogSeverity Severity { get; init; } = ClientLogSeverity.Information;

    public object? Context { get; init; } // objeto con datos extra del evento

    public DateTimeOffset? Timestamp { get; init; }
}

public enum ClientLogSeverity
{
    Trace, Debug, Information, Warning, Error, Critical
}
