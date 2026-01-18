using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.AccessFilter;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Subscriptions;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Controllers;

/// <summary>
/// Controlador responsable de exponer operaciones relacionadas con la gestión de suscripciones
/// de Microsoft Graph para monitorear mensajes en chats de Microsoft Teams.
/// </summary>
[Route("api")]
[ApiController]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    /// <summary>
    /// Inicializa una nueva instancia del controlador de suscripciones.
    /// </summary>
    /// <param name="subscriptionService">Servicio de negocio encargado de gestionar suscripciones de Graph.</param>
    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Obtiene la lista de suscripciones activas registradas actualmente en el tenant.
    /// </summary>
    /// <remarks>
    /// Este endpoint consulta directamente a Microsoft Graph.  
    /// No retorna datos desde la base de datos local.
    /// La paginación depende del Graph SDK.
    /// </remarks>
    /// <returns>Una lista de suscripciones activas envuelta en un DTO estándar.</returns>
    //[Authorize]
    //[ServiceFilter(typeof(UserAccessFilter))]
    [HttpGet("subscription/list")]
    public async Task<IActionResult> ListActiveSubscriptions()
    {
        // Se delega al servicio la obtención de suscripciones activas.
        var subscriptions = await _subscriptionService.ListActiveSubscriptionsAsync();

        return Ok(new WebApiResponseDto<List<Subscription>>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Suscripciones activas obtenidas exitosamente.",
            ResponseData = subscriptions
        });        
    }

    /// <summary>
    /// Crea una nueva suscripción en Microsoft Graph para un usuario específico,
    /// habilitando la recepción de notificaciones de mensajes de sus chats.
    /// </summary>
    /// <param name="entraUserId">Identificador Entra del usuario al que se asociará la suscripción.</param>
    /// <param name="request">Objeto que contiene el NotificationUrl requerido por Graph.</param>
    /// <returns>Información de la suscripción creada.</returns>
    //[Authorize]
    //[ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("subscription/{entraUserId}")]
    public async Task<IActionResult> CreateSubscription(string entraUserId, [FromBody] CreateSubscriptionRequest request)
    {
        // Validación básica del EntraUserId proporcionado.
        if (string.IsNullOrWhiteSpace(entraUserId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Se requiere el EntraUserId del usuario.",
                ResponseData = null
            });
        }

        // Validación del NotificationUrl requerido para que Graph envíe notificaciones.
        if (string.IsNullOrWhiteSpace(request.NotificationUrl))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Se requiere el NotificationUrl en el body.",
                ResponseData = null
            });
        }

        var subscription = await _subscriptionService.CreateSubscriptionAsync(entraUserId, request.NotificationUrl);

        return Ok(new WebApiResponseDto<Subscription>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Suscripción creada exitosamente.",
            ResponseData = subscription
        });
        
    }

    /// <summary>
    /// Activa las suscripciones de todos los mentores activos registrados en el sistema.
    /// </summary>
    /// <remarks>
    /// La activación se ejecuta en paralelo dentro del servicio para optimizar el rendimiento,
    /// especialmente útil cuando existen decenas o cientos de mentores.
    /// </remarks>
    /// <returns>Un diccionario indicando por cada usuario si su suscripción fue activada correctamente.</returns>
    //[Authorize]
    //[ServiceFilter(typeof(UserAccessFilter))]
    [HttpPost("subscription/activate")]
    public async Task<IActionResult> ActivateSubscriptions()
    {
        var result = await _subscriptionService.ActivateSubscriptionsAsync();

        return Ok(new WebApiResponseDto<Dictionary<string, bool>>
        {
            ResponseCode = ResponseTypeCodeDto.Ok,
            ResponseMessage = "Proceso de activación completado.",
            ResponseData = result
        });
    }

    /// <summary>
    /// Elimina una suscripción específica en Microsoft Graph mediante su identificador.
    /// </summary>
    /// <param name="subscriptionId">Identificador de la suscripción en Microsoft Graph.</param>
    /// <returns>Indica si la eliminación fue exitosa o si ocurrió un error.</returns>
    // [Authorize]
    // [ServiceFilter(typeof(UserAccessFilter))]
    [HttpDelete("subscription/{subscriptionId}")]
    public async Task<IActionResult> DeactivateSubscription(string subscriptionId)
    {
        // Validación básica antes de enviar la solicitud al servicio.
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return BadRequest(new WebApiResponseDto<object>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Se requiere el SubscriptionId.",
                ResponseData = null
            });
        }

        // Si el servicio devuelve false, significa que Graph no pudo eliminar la suscripción.
        var success = await _subscriptionService.DeactivateSubscriptionAsync(subscriptionId);

        if (success)
        {
            return Ok(new WebApiResponseDto<bool>
            {
                ResponseCode = ResponseTypeCodeDto.Ok,
                ResponseMessage = "Suscripción desactivada exitosamente.",
                ResponseData = true
            });
        }

        return StatusCode(StatusCodes.Status500InternalServerError,
            new WebApiResponseDto<bool>
            {
                ResponseCode = ResponseTypeCodeDto.Error,
                ResponseMessage = "Error al desactivar la suscripción.",
                ResponseData = false
            });
    }
}
