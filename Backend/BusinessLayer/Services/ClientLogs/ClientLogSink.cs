using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Microsoft.Extensions.Logging;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.ClientLogs;

/// <summary>
/// Implementación de <see cref="IClientLogSink"/> que envía los registros del cliente
/// directamente a la consola del servidor.
/// </summary>
public sealed class ClientLogSink : IClientLogSink
{
    private readonly ILogger<ClientLogSink> _logger;

    /// <summary>
    /// Inicializa el sink que imprime los logs del cliente en la consola.
    /// </summary>
    /// <param name="logger">Instancia de <see cref="ILogger"/> para registrar eventos internos si fuera necesario.</param>
    public ClientLogSink(ILogger<ClientLogSink> logger) => _logger = logger;

    /// <summary>
    /// Escribe un registro enviado por el cliente. El log se imprime en consola y se normaliza
    /// para evitar saltos de línea o tamaños excesivos en el campo de contexto.
    /// </summary>
    /// <param name="e">Entrada de log proveniente del cliente.</param>
    /// <param name="ct">Token para cancelar la operación si se requiere.</param>
    public Task WriteAsync(ClientLogEntry logEntry)
    {
        // Usa la marca de tiempo enviada por el frontend o, si no existe, la actual
        var logTimestamp = logEntry.Timestamp ?? DateTimeOffset.UtcNow;

        // Convierte el contexto en string de manera segura
        // Si es null → queda null
        // Si es string → se usa directamente
        // Si es un objeto → se usa ToString()
        string? normalizedContext = logEntry.Context switch
        {
            null => null,
            string s => s,
            object o => o.ToString()
        };

        // Normaliza el contexto eliminando saltos de línea y recortando excesos
        if (!string.IsNullOrWhiteSpace(normalizedContext))
        {
            // Evita que los logs generen múltiples líneas en consola
            normalizedContext = normalizedContext
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            const int maxContextLength = 3000;

            // Previene registros demasiado largos que podrían saturar la consola
            if (normalizedContext.Length > maxContextLength) 
                normalizedContext = normalizedContext.Substring(0, maxContextLength) + "…";

            Console.WriteLine(
                $"CLIENT LOG [{logTimestamp:O}] {logEntry.Severity} | user={logEntry.UserId} chat={logEntry.ChatId} | {logEntry.Message} | context={logTimestamp}");
        }
        else
        {
            // Registro sin contexto adicional
            Console.WriteLine(
                $"CLIENT LOG [{logTimestamp:O}] {logEntry.Severity} | user={logEntry.UserId} chat={logEntry.ChatId} | {logEntry.Message}");
        }

        return Task.CompletedTask;
    }
}
