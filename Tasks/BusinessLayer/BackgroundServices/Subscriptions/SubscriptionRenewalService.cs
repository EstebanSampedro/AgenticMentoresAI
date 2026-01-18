using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.BackgroundServices.Subscriptions;

public class SubscriptionRenewalService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public SubscriptionRenewalService(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("SubscriptionRenewalService iniciado.");

        var interval = TimeSpan.FromMinutes(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

                await maintenance.MaintainSubscriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SubscriptionRenewal] Error: {ex}");
            }

            // Delay hasta el próximo ciclo
            var delay = interval - (DateTime.UtcNow - startTime);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
