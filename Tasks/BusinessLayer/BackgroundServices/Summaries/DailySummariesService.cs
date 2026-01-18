using Academikus.AgenteInteligenteMentoresTareas.Business.Common.Time;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.DailySummaries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Summaries;

public sealed class DailySummariesService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public DailySummariesService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Punto de entrada de DailySummariesService que es un servicio en segundo.
    /// Se encarga de programar la ejecución periódica 
    /// de la generación de resúmenes diarios en la hora local configurada.
    /// </summary>
    /// <param name="stoppingToken">
    /// Token de cancelación utilizado para detener el servicio 
    /// de forma segura cuando la aplicación finaliza.
    /// </param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Validar si el servicio está habilitado vía configuración.
        // Permite desactivar la funcionalidad sin necesidad de desplegar una nueva versión.
        var dailySummariesEnabled = _configuration.GetValue("DailySummaries:Enabled", true);
        if (!dailySummariesEnabled)
        {
            Console.WriteLine("DailySummaries: deshabilitado por configuración.");
            return;
        }

        // Obtiene la zona horaria desde configuración o usa Ecuador como valor predeterminado.
        var configuredTimeZoneId = _configuration["DailySummaries:TimeZone"];
        var executionTimeZone = TimeZoneHelper.GetEcuadorTimeZone(configuredTimeZoneId);

        // Hora local de ejecución (HH:mm), por defecto 00:05 AM si no se especifica.
        // Permite ejecutar el job a una hora controlada cada día.
        var scheduledExecutionTime = 
            ParseTime(_configuration["DailySummaries:RunAt"]) ?? new TimeSpan(0, 5, 0);

        Console.WriteLine($"DailySummaries: Iniciado. TZ={executionTimeZone.Id} RunAt={scheduledExecutionTime}");

        // Ejecución inicial al arrancar el servicio.
        // Se ejecuta para el día anterior según la hora local.
        await ExecuteProcessorAsync(executionTimeZone, stoppingToken);

        // Bucle principal que espera hasta la próxima ejecución programada
        // calculada en base a la hora configurada.
        while (!stoppingToken.IsCancellationRequested)
        {
            // Tiempo restante hasta la próxima ejecución del proceso.
            var waitTimeBeforeNextExecution = 
                ComputeDelayToNextRun(executionTimeZone, scheduledExecutionTime);

            // Retrasa la ejecución hasta alcanzar el horario programado.
            await Task.Delay(waitTimeBeforeNextExecution, stoppingToken);

            // Ejecuta el procesamiento del resumen diario usando la lógica de negocio externa.
            await ExecuteProcessorAsync(executionTimeZone, stoppingToken);
        }
    }

    /// <summary>
    /// Resuelve el servicio encargado de procesar los resúmenes diarios
    /// y delega la ejecución del procesamiento al servicio especializado.
    /// El método crea un nuevo scope por ejecución para asegurar que los
    /// servicios Scoped como DBContext sean instanciados correctamente.
    /// </summary>
    /// <param name="timeZone">
    /// Zona horaria utilizada para calcular el día objetivo de los resúmenes.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelación del servicio en segundo plano.
    /// </param>
    private async Task ExecuteProcessorAsync(TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        // Crea un nuevo scope para esta ejecución,
        // asegurando ciclos limpios del DBContext y otros servicios Scoped.
        using var scope = _scopeFactory.CreateScope();

        // Obtiene la implementación del servicio que contiene la lógica de negocio
        // para generar los resúmenes del día anterior según la hora local configurada.
        var dailySummaryProcessor = scope.ServiceProvider.GetRequiredService<ISummaryService>();

        // Ejecuta el procesamiento real del negocio
        await dailySummaryProcessor.ExecuteProcessorAsync(timeZone, cancellationToken);
    }

    /// <summary>
    /// Calcula el intervalo de tiempo que falta para la próxima ejecución programada
    /// del procesamiento de resúmenes, tomando en cuenta la zona horaria local y
    /// la hora objetivo configurada.
    /// </summary>
    /// <param name="timeZone">
    /// Zona horaria en la que debe evaluarse el tiempo local para determinar el próximo disparo del proceso.
    /// </param>
    /// <param name="scheduledExecutionTimeLocal">
    /// Hora local del día en la cual debe ejecutarse el proceso diario (HH:mm).
    /// </param>
    /// <returns>
    /// Un <see cref="TimeSpan"/> indicando cuánto tiempo debe esperar el servicio antes de ejecutar nuevamente.
    /// </returns>
    private static TimeSpan ComputeDelayToNextRun(TimeZoneInfo timeZone, TimeSpan scheduledExecutionTimeLocal)
    {
        // Hora actual en UTC
        var currentUtcTime = DateTimeOffset.UtcNow;

        // Convertir la hora actual a la zona horaria local configurada
        var currentLocalTime = TimeZoneInfo.ConvertTime(currentUtcTime, timeZone);

        // Hora programada de ejecución correspondiente al día actual en horario local
        var scheduledExecutionTodayLocal = currentLocalTime.Date + scheduledExecutionTimeLocal;

        // Determina si ejecutar hoy o programar para mañana
        var nextExecutionLocalTime =
            (currentLocalTime <= scheduledExecutionTodayLocal)
                ? scheduledExecutionTodayLocal
                : scheduledExecutionTodayLocal.AddDays(1);

        // Convertir el horario objetivo a UTC, respetando el desfase horario del huso configurado
        var nextExecutionUtcTime = new DateTimeOffset(
            nextExecutionLocalTime,
            timeZone.GetUtcOffset(nextExecutionLocalTime)
        ).ToUniversalTime();

        // Calcular cuánto tiempo esperar
        var waitTimeBeforeNextExecution = nextExecutionUtcTime - currentUtcTime;

        return waitTimeBeforeNextExecution <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1)
            : waitTimeBeforeNextExecution;
    }

    /// <summary>
    /// Intenta convertir una cadena con formato de hora local (HH:mm)
    /// en un <see cref="TimeSpan"/> que representa la hora configurada
    /// para ejecutar el proceso diario.
    /// </summary>
    /// <param name="timeString">
    /// Cadena con la hora y minuto, en formato <c>"HH:mm"</c>. 
    /// Si es nula, vacía o inválida, se retorna <c>null</c>.
    /// </param>
    /// <returns>
    /// Objeto <see cref="TimeSpan"/> con la hora extraída o <c>null</c>
    /// si el formato no es válido.
    /// </returns>
    private static TimeSpan? ParseTime(string? timeString)
    {
        // Si no hay valor en configuración o contiene solo espacios, se considera inválido.
        if (string.IsNullOrWhiteSpace(timeString)) return null;

        // Se valida el formato mediante expresión regular:
        // - Horas: uno o dos dígitos (00-23)
        // - Minutos: exactamente dos dígitos (00-59)
        var match = System.Text.RegularExpressions.Regex.Match(timeString, @"^(?<h>\d{1,2}):(?<m>\d{2})$");
        if (!match.Success) 
            return null;

        // Se extraen los valores numéricos del match ya validado
        var hour = int.Parse(match.Groups["h"].Value);
        var minute = int.Parse(match.Groups["m"].Value);

        // Se construye el TimeSpan resultante representando la hora local
        return new TimeSpan(hour, minute, 0);
    }
}
