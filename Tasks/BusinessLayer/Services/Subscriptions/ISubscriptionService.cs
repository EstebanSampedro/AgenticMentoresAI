using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Subscriptions;

public interface ISubscriptionService
{
    /// <summary>
    /// Lista todas las suscripciones activas en Microsoft Graph.
    /// </summary>
    Task<List<Subscription>> ListActiveSubscriptionsAsync();

    /// <summary>
    /// Renueva una suscripción existente con una nueva fecha de expiración.
    /// </summary>
    /// <param name="subscriptionId">ID de la suscripción a renovar.</param>
    /// <returns>La suscripción renovada.</returns>
    Task<Subscription?> RenewSubscriptionAsync(string subscriptionId);

    /// <summary>
    /// Crea una nueva suscripción a mensajes de Teams para el usuario indicado.
    /// </summary>
    /// <param name="userId">ID del usuario en Entra ID (AAD).</param>
    /// <param name="notificationUrl">URL donde Microsoft Graph enviará notificaciones.</param>
    /// <returns>La suscripción recién creada.</returns>
    Task<Subscription?> CreateSubscriptionAsync(string userId, string notificationUrl);

    /// <summary>
    /// Busca una suscripción activa por recurso exacto (usualmente: users/{userId}/chats/getAllMessages).
    /// </summary>
    /// <param name="resource">Ruta exacta del recurso en Graph.</param>
    /// <returns>La suscripción encontrada o null.</returns>
    Task<Subscription?> FindActiveSubscriptionByResourceAsync(string resource);

    Task<bool> DeactivateSubscriptionAsync(string subscriptionId);

    Task MaintainSubscriptionsAsync(CancellationToken cancellationToken);
}
