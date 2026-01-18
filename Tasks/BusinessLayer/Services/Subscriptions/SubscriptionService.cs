using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Subscriptions;
using Academikus.AgenteInteligenteMentoresTareas.Business.Services.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Repositories;

public class SubscriptionService : ISubscriptionService
{
    private readonly GraphServiceClient _graphClient;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;

    public SubscriptionService(
        GraphServiceClient graphClient, 
        IUserService userService,
        IConfiguration configuration)
    {
        _graphClient = graphClient;
        _userService = userService;
        _configuration = configuration;
    }

    public async Task<List<Subscription>> ListActiveSubscriptionsAsync()
    {
        try
        {
            var clientId = _configuration["AzureAd:ClientId"];

            var response = await _graphClient.Subscriptions.GetAsync();

            var filtered = response?.Value?
                .Where(s => s.ApplicationId != null && s.ApplicationId.Equals(clientId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return response?.Value?.ToList() ?? new List<Subscription>();
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error al listar suscripciones: {ex.Message}");

            return new List<Subscription>();
        }
    }

    public async Task<Subscription?> RenewSubscriptionAsync(string subscriptionId)
    {
        try
        {
            var subscriptionUpdate = new Subscription
            {
                ExpirationDateTime = DateTime.UtcNow.AddMinutes(59)
            };

            var renewed = await _graphClient.Subscriptions[subscriptionId].PatchAsync(subscriptionUpdate);
            return renewed;
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error al renovar suscripción {subscriptionId}: {ex.Message}");
            return null;
        }
    }

    public async Task<Subscription?> CreateSubscriptionAsync(string userId, string notificationUrl)
    {
        var resource = $"users/{userId}/chats/getAllMessages?model=B";

        var subscription = new Subscription
        {
            ChangeType = "created,updated,deleted",
            NotificationUrl = notificationUrl,
            Resource = resource,
            ExpirationDateTime = DateTime.UtcNow.AddMinutes(59),
            ClientState = "secretClientValue"
        };

        try
        {
            var result = await _graphClient.Subscriptions.PostAsync(subscription);
            return result;
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error al crear suscripción para {userId}: {ex.Message}");
            return null;
        }
    }

    public async Task<Subscription?> FindActiveSubscriptionByResourceAsync(string resource)
    {
        try
        {
            var subscriptions = await _graphClient.Subscriptions.GetAsync();
            return subscriptions?.Value?
                .FirstOrDefault(s => s.Resource?.Equals(resource, StringComparison.OrdinalIgnoreCase) == true);
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error al buscar suscripción para {resource}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeactivateSubscriptionAsync(string subscriptionId)
    {
        try
        {
            await _graphClient.Subscriptions[subscriptionId].DeleteAsync();

            return true;
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error al eliminar suscripción: {ex.Message}");

            return false;
        }
    }

    public async Task MaintainSubscriptionsAsync(CancellationToken cancellationToken)
    {
        // Traer suscripciones activas desde Graph
        var subscriptions = await ListActiveSubscriptionsAsync();

        // Obtener mentores activos desde la BD
        var activeMentorIds = (await _userService.GetActiveMentorEntraIdsAsync())
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Obtener mentores inactivos desde la BD
        var inactiveMentorIds = (await _userService.GetInactiveMentorEntraIdsAsync())
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Desactivar suscripciones de mentores inactivos
        foreach (var sub in subscriptions)
        {
            // Solo nos interesan las suscripciones que son de "users/{id}/chats/getAllMessages?model=B"
            var ownerId = TryExtractUserIdFromResource(sub.Resource);

            if (string.IsNullOrWhiteSpace(ownerId))
                continue;

            if (inactiveMentorIds.Contains(ownerId))
            {
                var ok = await DeactivateSubscriptionAsync(sub.Id);

                Console.WriteLine(ok
                    ? $"Suscripción desactivada (mentor inactivo) para {ownerId} - SubId: {sub.Id}"
                    : $"Error desactivando suscripción para {ownerId} - SubId: {sub.Id}");

                continue;
            }
        }

        foreach (var sub in subscriptions)
        {
            if (sub?.Id == null || sub.ExpirationDateTime == null || string.IsNullOrWhiteSpace(sub.Resource))
                continue;

            var ownerId = TryExtractUserIdFromResource(sub.Resource);
            if (!string.IsNullOrWhiteSpace(ownerId) && inactiveMentorIds.Contains(ownerId))
                continue; // no renovar inactivos

            var remaining = sub.ExpirationDateTime.Value.ToUniversalTime() - DateTime.UtcNow;
            if (remaining < TimeSpan.FromMinutes(30))
            {
                await RenewSubscriptionAsync(sub.Id);
            }
        }

        foreach (var mentorId in activeMentorIds)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // Seguridad extra: si por algún bug está en ambos sets, prioriza inactivo
            if (inactiveMentorIds.Contains(mentorId))
                continue;

            var resource = $"users/{mentorId}/chats/getAllMessages?model=B";
            var existing = await FindActiveSubscriptionByResourceAsync(resource);

            if (existing == null)
            {
                var endpoint = _configuration["AzureAd:NotificationUrl"];
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    Console.WriteLine("AzureAd:NotificationUrl no configurado");
                    continue;
                }

                var created = await CreateSubscriptionAsync(mentorId, endpoint);
                Console.WriteLine(created != null
                    ? $"Suscripción creada para {mentorId}"
                    : $"Error creando suscripción para {mentorId}");
            }
        }
    }

    /// <summary>
    /// Extrae el userId de un resource tipo: "users/{id}/chats/getAllMessages?model=B"
    /// Devuelve null si no matchea.
    /// </summary>
    private static string? TryExtractUserIdFromResource(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
            return null;

        // Esperado: users/{id}/...
        // Split simple, robusto y sin regex.
        var parts = resource.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        if (!parts[0].Equals("users", StringComparison.OrdinalIgnoreCase))
            return null;

        return parts[1];
    }
}
