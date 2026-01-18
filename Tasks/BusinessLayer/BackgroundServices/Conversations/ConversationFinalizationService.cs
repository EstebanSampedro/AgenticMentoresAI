using Academikus.AgenteInteligenteMentoresTareas.Business.Services.ConversationLifecycle;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Conversations;

/// <summary>
/// Servicio en segundo plano responsable de verificar periódicamente la inactividad 
/// en conversaciones activas y marcarlas como finalizadas si corresponde.
/// </summary>
public class ConversationFinalizationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConversationTimeoutOptions _conversationTimeoutOptions;

    /// <summary>
    /// Inicializa el servicio de finalización de conversaciones.
    /// </summary>
    /// <param name="serviceProvider">
    /// Proveedor de servicios para crear scopes y resolver dependencias por ciclo.
    /// </param>
    /// <param name="conversationTimeoutOptions">
    /// Configuración proveniente de appsettings que define tiempos de inactividad 
    /// y frecuencia de ejecución del proceso.
    /// </param>
    public ConversationFinalizationService(
        IServiceProvider serviceProvider,
        IOptions<ConversationTimeoutOptions> conversationTimeoutOptions)
    {
        _serviceProvider = serviceProvider;
        _conversationTimeoutOptions = conversationTimeoutOptions.Value;
    }

    /// <summary>
    /// Método ejecutado automáticamente por el runtime en un hilo en segundo plano.
    /// Mantiene en ejecución el ciclo de revisión de inactividad hasta que la aplicación se detiene.
    /// </summary>
    /// <param name="stoppingToken">Token de cancelación enviado cuando se detiene el servicio.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ciclo de ejecución constante mientras el servicio no reciba señal de cancelación
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Se crea un scope por ciclo para obtener instancias de servicios con lifetime Scoped
                using var scope = _serviceProvider.CreateScope();

                // Servicio que contiene la lógica de negocio para determinar y cerrar conversaciones inactivas
                var lifecycleService = scope.ServiceProvider
                    .GetRequiredService<IConversationLifecycleService>();

                // Ejecuta la finalización de conversaciones inactivas
                var finalizedCount = await lifecycleService
                    .FinalizeInactiveConversationsAsync(stoppingToken);

                Console.WriteLine(
                    $"Conversaciones finalizadas por inactividad: {finalizedCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al finalizar conversaciones por inactividad. {ex}");
            }

            // Espera controlada entre ejecuciones del ciclo, basada en variables de configuración
            await Task.Delay(
                TimeSpan.FromMinutes(_conversationTimeoutOptions.CheckIntervalMinutes),
                stoppingToken);
        }
    }
}
