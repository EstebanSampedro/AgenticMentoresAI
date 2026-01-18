using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Subscriptions;

/// <summary>
/// Servicio encargado de gestionar las suscripciones de Microsoft Graph relacionadas
/// con los mensajes de chat de los mentores. Permite crear, listar, activar y desactivar
/// suscripciones para chats de usuarios en Microsoft Teams.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly string _notificationUrl;

    private readonly DBContext _context;
    private readonly GraphServiceClient _graphClient;

    private readonly IConfiguration _configuration;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de suscripciones.
    /// </summary>
    /// <param name="context">Contexto de base de datos utilizado para consultar usuarios.</param>
    /// <param name="graphClient">Cliente Microsoft Graph autenticado.</param>
    /// <param name="configuration">Proveedor de configuración del sistema.</param>
    /// <exception cref="InvalidOperationException">Se lanza si falta la URL de notificación.</exception>
    public SubscriptionService(
        DBContext context,
        GraphServiceClient graphClient,
        IConfiguration configuration)
    {
        _context = context;
        _graphClient = graphClient;
        _configuration = configuration;

        // Se valida que la URL de notificación esté configurada para evitar errores silenciosos en tiempo de ejecución.
        _notificationUrl = configuration["AzureAd:NotificationUrl"]
            ?? throw new InvalidOperationException("Variable de configuración NotificationUrl no configurada.");
    }

    /// <summary>
    /// Obtiene los EntraUserId de todos los mentores activos registrados en la base de datos.
    /// </summary>
    /// <returns>Lista de identificadores EntraUserId de mentores activos.</returns>
    public async Task<List<string?>> GetActiveMentorEntraUserIdsAsync()
    {
        return await _context.UserTables
            .Where(u => u.UserRole == "Mentor" && u.UserState == "Activo")
            .Select(u => u.EntraUserId)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene el listado actual de suscripciones activas en Microsoft Graph.
    /// Este método consulta directamente las suscripciones en Graph, no en la base de datos local.
    /// </summary>
    /// <returns>Lista de objetos Subscription proporcionados por Microsoft Graph.</returns>
    public async Task<List<Subscription>> ListActiveSubscriptionsAsync()
    {
        // Se realiza una consulta directa a Graph. Si no retorna resultados,
        // se inicializa una lista vacía para evitar errores de referencia nula.
        var subscriptions = await _graphClient.Subscriptions.GetAsync();

        return subscriptions?.Value?.ToList() ?? new List<Subscription>();
    }

    /// <summary>
    /// Activa suscripciones para todos los mentores activos. Cada activación se ejecuta en paralelo
    /// para mejorar el rendimiento total.
    /// </summary>
    /// <returns>
    /// Diccionario donde la clave es el EntraUserId del mentor y el valor indica si la creación fue exitosa.
    /// </returns>
    public async Task<Dictionary<string, bool>> ActivateSubscriptionsAsync()
    {
        var mentorIds = await GetActiveMentorEntraUserIdsAsync();

        // Se genera una colección de tareas para ejecutar la activación de manera concurrente.
        var activationTasks = mentorIds.Select(async id =>
        {
            // Para cada mentor se intenta activar su suscripción.
            var success = await ActivateSubscriptionForMentorAsync(id);
            return (id, success);
        });

        // Espera a que todas las tareas se completen.
        var resultsArray = await Task.WhenAll(activationTasks);

        // Conversión a diccionario para una lectura más clara desde el controlador o servicio llamante.
        return resultsArray.ToDictionary(x => x.id, x => x.success);
    }

    /// <summary>
    /// Activa una suscripción de Graph para un mentor específico.
    /// </summary>
    /// <param name="entraUserId">Identificador Entra del mentor.</param>
    /// <returns>True si la suscripción se creó exitosamente; de lo contrario false.</returns>
    public async Task<bool> ActivateSubscriptionForMentorAsync(string entraUserId)
    {
        try
        {
            // Se delega en CreateSubscriptionAsync para minimizar duplicación de lógica.
            var subscription = await CreateSubscriptionAsync(entraUserId, _notificationUrl);

            return subscription != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al activar la suscripción para el usuario {entraUserId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Crea una suscripción en Microsoft Graph para monitorear mensajes del usuario especificado.
    /// </summary>
    /// <param name="userId">EntraUserId del mentor para quien se crea la suscripción.</param>
    /// <param name="notificationUrl">URL donde Microsoft Graph enviará las notificaciones.</param>
    /// <returns>La suscripción creada.</returns>
    /// <exception cref="ServiceException">Se lanza si Graph devuelve un error al crear la suscripción.</exception>
    public async Task<Subscription> CreateSubscriptionAsync(string userId, string notificationUrl)
    {
        var clientState = _configuration["AzureAd:WebhookClientState"];

        // Los campos configurados definen el comportamiento de la suscripción.
        // Se utiliza un tiempo de expiración de 59 minutos para cumplir con las reglas actuales de Graph.
        var subscription = new Subscription
        {
            ChangeType = "created,updated,deleted",
            NotificationUrl = notificationUrl,
            Resource = $"users/{userId}/chats/getAllMessages?model=B",
            ExpirationDateTime = DateTime.UtcNow.AddMinutes(59),
            ClientState = clientState
        };

        try
        {
            // Envío de la suscripción a Microsoft Graph.
            var newSubscription = await _graphClient.Subscriptions.PostAsync(subscription);

            return newSubscription!;
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error al crear la suscripción: {ex.Message}");
            throw;
        }
    }
      
    /// <summary>
    /// Desactiva (elimina) una suscripción existente en Microsoft Graph.
    /// </summary>
    /// <param name="subscriptionId">Identificador interno de la suscripción en Graph.</param>
    /// <returns>True si la eliminación fue exitosa; false en caso de error.</returns>
    public async Task<bool> DeactivateSubscriptionAsync(string subscriptionId)
    {
        try
        {
            // Se elimina la suscripción directamente desde Graph.
            await _graphClient.Subscriptions[subscriptionId].DeleteAsync();

            return true;
        }
        catch (ServiceException ex)
        {
            Console.WriteLine($"Error al eliminar suscripción: {ex.Message}");
            return false;
        }
    }
}
