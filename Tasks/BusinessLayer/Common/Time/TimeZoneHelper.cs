namespace Academikus.AgenteInteligenteMentoresTareas.Business.Common.Time;

/// <summary>
/// Utilidades para manejo de zonas horarias y conversiones de fechas
/// hacia UTC respetando reglas de Ecuador.
/// </summary>
public static class TimeZoneHelper
{
    /// <summary>
    /// Obtiene la zona horaria de Ecuador usando el ID pasado por parámetro
    /// o aplicando automáticamente el valor correcto dependiendo del sistema operativo.
    /// </summary>
    /// <param name="tzId">
    /// Identificador de zona horaria personalizado. Si es null o vacío,
    /// se aplicará el identificador apropiado según el entorno.
    /// </param>
    /// <returns>Instancia válida de <see cref="TimeZoneInfo"/>.</returns>
    public static TimeZoneInfo GetEcuadorTimeZone(string? tzId)
    {
        try
        {
            // Si envían un ID válido se respeta
            if (!string.IsNullOrWhiteSpace(tzId))
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);

            // Preferido para Linux
            return TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
        }
        catch
        {
            try
            {
                // Fallback para Windows
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TimeZone] No se pudo resolver zona horaria: {ex}");
                throw;
            }
        }
    }

    /// <summary>
    /// Devuelve el rango UTC equivalente al día local especificado.
    /// Se usa para consultas de datos con ventanas precisas por fecha.
    /// </summary>
    /// <param name="localDay">Fecha local.</param>
    /// <param name="tz">Zona horaria del día.</param>
    /// <returns>Tupla con inicio y fin del día en UTC.</returns>
    /// <exception cref="ArgumentNullException">Si tz es null.</exception>
    /// <exception cref="ArgumentException">Si localDay es inválido.</exception>
    public static (DateTimeOffset startUtc, DateTimeOffset endUtc)
        GetUtcWindowForLocalDay(DateOnly localDay, TimeZoneInfo tz)
    {
        if (tz is null)
        {
            Console.WriteLine("[TimeZone] Zona horaria nula en GetUtcWindowForLocalDay.");
            throw new ArgumentNullException(nameof(tz));
        }

        if (localDay == default)
        {
            Console.WriteLine("[TimeZone] Parámetro localDay inválido.");
            throw new ArgumentException("Fecha local inválida.", nameof(localDay));
        }

        // Construcción a medianoche con base en la zona horaria
        var startLocal = localDay.ToDateTime(TimeOnly.MinValue);
        var nextLocal = localDay.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Incluye información de DST (horario de verano si aplica)
        var startOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
        var endOffset = new DateTimeOffset(nextLocal, tz.GetUtcOffset(nextLocal));

        // Conversión a fechas absolutas UTC
        var startUtc = startOffset.ToUniversalTime();
        var endUtc = endOffset.ToUniversalTime();

        return (startUtc, endUtc);
    }
}
