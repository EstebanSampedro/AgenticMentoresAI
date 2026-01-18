using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Users;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Users;

public class StudentInfoRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly StudentInformationRefreshOptions _studentInformationRefreshOptions;

    private DateTime _lastRunUtc = DateTime.MinValue;

    public StudentInfoRefreshService(
        IServiceProvider serviceProvider,
        IOptions<StudentInformationRefreshOptions> studentInformationRefreshOptions)
    {
        _serviceProvider = serviceProvider;
        _studentInformationRefreshOptions = studentInformationRefreshOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("StudentInfoRefreshService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Si la funcionalidad está deshabilitada vía configuración, esperar y seguir
                if (!_studentInformationRefreshOptions.Enabled)
                {
                    await Task.Delay(TimeSpan.FromMinutes(_studentInformationRefreshOptions.CheckIntervalMinutes), stoppingToken);
                    continue;
                }

                var now = DateTime.UtcNow;
                var isExecutionDue = (_lastRunUtc == DateTime.MinValue) ||
                                    (now - _lastRunUtc >= TimeSpan.FromDays(_studentInformationRefreshOptions.IntervalDays));

                if (!isExecutionDue)
                {
                    await Task.Delay(TimeSpan.FromMinutes(_studentInformationRefreshOptions.CheckIntervalMinutes), stoppingToken);
                    continue;
                }

                // Ejecutar la sincronización dentro de un scope nuevo
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IUserService>();

                var stopwatch = Stopwatch.StartNew();

                // Ejecuta sincronización de datos de Banner
                var (processed, updatedCount) = await syncService.SyncStudentInfoAsync(stoppingToken);

                stopwatch.Stop();

                _lastRunUtc = DateTime.UtcNow;

                Console.WriteLine($"[StudentInfoRefresh] Procesados: {processed}, Actualizados: {updatedCount}, Tiempo: {stopwatch.Elapsed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentInfoRefresh] Error general: {ex}");
            }

            // Espera antes de volver a evaluar si “ya toca”
            await Task.Delay(TimeSpan.FromMinutes(_studentInformationRefreshOptions.CheckIntervalMinutes), stoppingToken);
        }
    }    
}
