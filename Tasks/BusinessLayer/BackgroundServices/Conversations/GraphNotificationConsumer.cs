using Academikus.AgenteInteligenteMentoresTareas.Business.Services.GraphNotification;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Conversations;

public class GraphNotificationConsumer : BackgroundService
{
    // Cliente del Event Hub encargado de procesar mensajes en particiones distribuidas
    private readonly EventProcessorClient _processor;

    // Service Provider utilizado para resolver servicios scoped dentro de un BackgroundService
    private readonly IServiceProvider _serviceProvider;

    public GraphNotificationConsumer(
        EventProcessorClient processor,
        IServiceProvider serviceProvider)
    {
        _processor = processor;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Inicia el procesamiento de eventos provenientes de Event Hubs
    /// y registra los manejadores de eventos y errores.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Se suscriben los handlers que procesarán eventos y errores globales
        _processor.ProcessEventAsync += HandleEventAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        // Inicia el procesamiento continuo de mensajes
        await _processor.StartProcessingAsync(stoppingToken);
    }

    /// <summary>
    /// Lógica del procesamiento de un evento individual recibido desde Event Hubs.
    /// </summary>
    private async Task HandleEventAsync(ProcessEventArgs args)
    {
        try
        {
            var json = args.Data.EventBody.ToString();

            // Microsoft Graph envía una notificación de validación al crear la suscripción.
            // Se ignora y se confirma checkpoint inmediatamente.
            if (IsValidationNotification(json))
            {
                // Console.WriteLine("Notificación de validación ignorada.");
                await args.UpdateCheckpointAsync();
                return;
            }

            // Procesa la notificación usando los servicios de Graph implementados
            await ProcessGraphNotificationAsync(json);

            // Confirma el procesamiento para que Event Hubs avance el offset
            await args.UpdateCheckpointAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error procesando evento: {ex}");
        }
    }

    /// <summary>
    /// Determina si el evento corresponde a una notificación
    /// de verificación de alcance enviada por Microsoft Graph.
    /// </summary>
    private static bool IsValidationNotification(string json)
    {
        return json.Contains("Validation: Testing client application reachability");
    }

    /// <summary>
    /// Deserializa el mensaje recibido y utiliza un servicio Scoped
    /// para manejar cada notificación individual.
    /// </summary>
    private async Task ProcessGraphNotificationAsync(string json)
    {
        var envelope = JsonConvert.DeserializeObject<NotificationModel>(json);

        // Se crea un scope para poder resolver servicios Scoped dentro de un BackgroundService
        using var scope = _serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IGraphNotificationService>();

        // Procesa cada notificación contenida en el paquete recibido
        foreach (var notif in envelope!.Value)
        {
            await processor.ProcessNotificationAsync(notif);
        }
    }

    /// <summary>
    /// Se ejecuta cuando se produce un error general del EventProcessorClient.
    /// Logs para diagnóstico y continuidad del servicio.
    /// </summary>
    private static Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        Console.WriteLine($"Error general del EventProcessorClient. {args.Exception}");
        return Task.CompletedTask;
    }
}
