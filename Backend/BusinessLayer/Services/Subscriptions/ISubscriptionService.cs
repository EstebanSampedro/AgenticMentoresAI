using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Subscriptions;

/// <summary>
/// Define las operaciones necesarias para gestionar suscripciones de Microsoft Graph 
/// relacionadas con la recepción de mensajes en chats de Microsoft Teams.
/// Permite crear, activar, desactivar y listar suscripciones, así como obtener 
/// los identificadores Entra de los mentores activos.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Obtiene el listado de EntraUserId correspondientes a los mentores activos 
    /// registrados en la base de datos.
    /// </summary>
    /// <returns>
    /// Lista de cadenas que representan los EntraUserId de los mentores activos.
    /// </returns>
    Task<List<string?>> GetActiveMentorEntraUserIdsAsync();

    /// <summary>
    /// Recupera todas las suscripciones activas actualmente registradas en Microsoft Graph.
    /// Este método consulta directamente las suscripciones remotas, no las de la base de datos local.
    /// </summary>
    /// <returns>
    /// Lista de objetos <see cref="Subscription"/> devueltos por Microsoft Graph.
    /// </returns>
    Task<List<Subscription>> ListActiveSubscriptionsAsync();
       
    /// <summary>
    /// Activa suscripciones para todos los mentores activos registrados en la base de datos.
    /// Cada activación se ejecuta de manera paralela para optimizar el rendimiento.
    /// </summary>
    /// <returns>
    /// Un diccionario donde la clave es el EntraUserId del mentor y el valor indica 
    /// si la activación fue exitosa.
    /// </returns>
    Task<Dictionary<string, bool>> ActivateSubscriptionsAsync();

    /// <summary>
    /// Activa una suscripción individual de Microsoft Graph para el mentor especificado.
    /// </summary>
    /// <param name="entraUserId">Identificador Entra del mentor para quien se creará la suscripción.</param>
    /// <returns>
    /// True si la suscripción fue creada exitosamente; false si ocurrió algún error.
    /// </returns>
    Task<bool> ActivateSubscriptionForMentorAsync(string entraUserId);

    /// <summary>
    /// Crea una nueva suscripción en Microsoft Graph para monitorear mensajes 
    /// de un usuario específico (por ejemplo, un mentor).
    /// </summary>
    /// <param name="userId">El identificador Entra del usuario al que se enlazará la suscripción.</param>
    /// <param name="notificationUrl">La URL pública a la que Microsoft Graph enviará las notificaciones.</param>
    /// <returns>
    /// Un objeto <see cref="Subscription"/> que representa la suscripción recién creada.
    /// </returns>
    Task<Subscription> CreateSubscriptionAsync(string userId, string notificationUrl);

    /// <summary>
    /// Elimina una suscripción existente en Microsoft Graph mediante su identificador.
    /// </summary>
    /// <param name="subscriptionId">Identificador único de la suscripción en Microsoft Graph.</param>
    /// <returns>
    /// True si la suscripción se eliminó correctamente; false si ocurrió un error.
    /// </returns>
    Task<bool> DeactivateSubscriptionAsync(string subscriptionId);
}
