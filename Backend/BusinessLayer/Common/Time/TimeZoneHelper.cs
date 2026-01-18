namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Time;

/// <summary>
/// Proporciona utilidades para manejo de zonas horarias,
/// especialmente para operaciones basadas en zona local (Ecuador).
/// </summary>
public static class TimeZoneHelper
{
    /// <summary>
    /// Obtiene la zona horaria de Ecuador basada en su identificador IANA.
    /// En Windows se aplica automáticamente un identificador alternativo
    /// si el principal no está disponible.
    /// </summary>
    /// <param name="overrideId">
    /// Identificador de zona horaria personalizado.  
    /// Si no se especifica, se usará el valor por defecto "America/Guayaquil".
    /// </param>
    /// <returns>Instancia válida de <see cref="TimeZoneInfo"/>.</returns>
    public static TimeZoneInfo GetEcuadorTimeZone(string? overrideId = null)
    {
        var id = string.IsNullOrWhiteSpace(overrideId) ? "America/Guayaquil" : overrideId;

        try 
        {
            // Intenta buscar la zona horaria por ID IANA
            return TimeZoneInfo.FindSystemTimeZoneById(id); 
        }
        catch (TimeZoneNotFoundException)
        {
            Console.WriteLine("[TimeZoneHelper] Zona IANA no disponible. Aplicando fallback...");
        }
        catch (InvalidTimeZoneException)
        {
            Console.WriteLine("[TimeZoneHelper] Zona IANA inválida. Aplicando fallback...");
        }

        // Fallback para sistemas Windows
        return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
    }

    /// <summary>
    /// Obtiene la fecha actual según la zona horaria especificada.
    /// </summary>
    /// <param name="tz">Zona horaria a utilizar.</param>
    /// <returns>Fecha local representada como DateOnly.</returns>
    public static DateOnly TodayInZone(TimeZoneInfo tz)
    {
        if (tz is null)
            throw new ArgumentNullException(nameof(tz));

        // Convierte la hora UTC actual a la hora local según la zona
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        return DateOnly.FromDateTime(nowLocal.Date);
    }

    /// <summary>
    /// Calcula el intervalo UTC que corresponde al día completo en la zona local dada.
    /// Se utiliza para consultas de datos en ventanas de tiempo precisas.
    /// </summary>
    /// <param name="localDay">Día local solicitado.</param>
    /// <param name="tz">Zona horaria del día.</param>
    /// <returns>
    /// Tupla con inicio y fin del día en UTC: StartUtc y EndUtc.
    /// </returns>
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc)
        GetUtcWindowForLocalDay(DateOnly localDay, TimeZoneInfo tz)
    {
        if (tz is null) 
            throw new ArgumentNullException(nameof(tz));

        if (localDay == default) 
            throw new ArgumentException("localDay inválido", nameof(localDay));

        // Construye la medianoche local con Kind=Unspecified para respetar reglas TZ
        var startLocal = new DateTime(localDay.Year, localDay.Month, localDay.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);

        // Conversión correcta a UTC según reglas de zona horaria
        var startUtcDt = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var endUtcDt = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);

        return (new DateTimeOffset(startUtcDt, TimeSpan.Zero),
                new DateTimeOffset(endUtcDt, TimeSpan.Zero));
    }

    /// <summary>
    /// Determina la fecha local del día anterior, según la zona horaria indicada.
    /// </summary>
    /// <param name="tz">Zona horaria a considerar.</param>
    /// <param name="utcNow">Fecha UTC opcional para pruebas o escenarios deterministas.</param>
    /// <returns>DateOnly correspondiente al día anterior local.</returns>
    public static DateOnly GetYesterdayLocal(TimeZoneInfo tz, DateTimeOffset? utcNow = null)
    {
        if (tz is null)
            throw new ArgumentNullException(nameof(tz));

        // Usa la fecha proporcionada para pruebas, o la fecha real del sistema
        var nowLocal = TimeZoneInfo.ConvertTime(utcNow ?? DateTimeOffset.UtcNow, tz);
        return DateOnly.FromDateTime(nowLocal.Date.AddDays(-1));
    }
}
