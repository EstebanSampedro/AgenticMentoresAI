using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Mentor;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Sessions;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Images;

/// <summary>
/// Servicio responsable de obtener imágenes almacenadas como hostedContents en Microsoft Teams,
/// utilizando sesiones de larga duración (LRO) y el flujo Long-Running OBO (AcquireTokenInLongRunningProcess).
/// </summary>
public class ImageService : IImageService
{
    private readonly string[] _scopes;

    // Cliente HTTP reutilizable (inyectado mediante IHttpClientFactory)
    private readonly HttpClient _httpClient;

    // Servicio que determina qué mentor es dueño de un chat
    private readonly IMentorLookupService _mentorLookupService;

    // Cliente MSAL configurado para long-running OBO
    private readonly IConfidentialClientApplication _confidentialClient;

    // Repositorio que administra sesiones persistentes de larga duración
    private readonly ILroSessionService _lroSessionRepository;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de imágenes.
    /// </summary>
    /// <param name="httpFactory">Fábrica de HttpClient utilizada para crear clientes configurados.</param>
    /// <param name="mentorLookupService">Servicio para resolver el UPN del mentor dueño del chat.</param>
    /// <param name="tokenStoreService">Servicio de almacenamiento de tokens (actualmente no utilizado aquí pero requerido por DI).</param>
    /// <param name="confidentialClient">Instancia de MSAL configurada para long-running OBO.</param>
    /// <param name="lroSessionRepository">Repositorio que administra sesiones LRO persistidas en base de datos.</param>
    /// <param name="configuration">Configuración general de la aplicación.</param>
    public ImageService(
        IHttpClientFactory httpFactory,
        IMentorLookupService mentorLookupService,
        ITokenStoreService tokenStoreService,
        IConfidentialClientApplication confidentialClient,
        ILroSessionService lroSessionRepository,
        IConfiguration configuration)
    {
        // Se obtiene un HttpClient configurado para Microsoft Graph mediante IHttpClientFactory
        _httpClient = httpFactory.CreateClient("GraphClient");

        _mentorLookupService = mentorLookupService;
        _confidentialClient = confidentialClient;
        _lroSessionRepository = lroSessionRepository;

        // Lectura de los scopes desde configuración
        _scopes = (configuration["Graph:Scopes"] ?? "Chat.ReadWrite offline_access")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Obtiene la imagen de un hostedContent en un mensaje de Teams,
    /// utilizando el token generado por la sesión de larga duración del mentor.
    /// </summary>
    /// <param name="chatId">Identificador del chat de Teams.</param>
    /// <param name="messageId">Identificador del mensaje que contiene el hostedContent.</param>
    /// <param name="contentId">Identificador del hostedContent a obtener.</param>
    /// <returns>
    /// Un objeto <see cref="GraphImageResponse"/> con los bytes y el tipo del contenido,
    /// o null si no se puede obtener la imagen o si la sesión requiere reautenticación.
    /// </returns>
    public async Task<GraphImageResponse?> GetHostedContentImageAsync(string chatId, string messageId, string contentId)
    {
        // Resolver el UPN del mentor asociado al chat.
        // Esto garantiza que el token solicitado pertenezca al usuario dueño del chat.
        var upn = await _mentorLookupService.GetMentorEmailByChatIdAsync(chatId);
        if (string.IsNullOrWhiteSpace(upn)) 
            return null;

        // Recuperar la sesión activa registrada en la base de datos.
        // Si no existe, el mentor debe abrir el tab de Teams para reactivar su sesión LRO.
        var session = await _lroSessionRepository.GetActiveByEmailAsync(upn);
        if (session is null)
            return null;

        try
        {
            // Solicitar un token fresco utilizando el flujo Long-Running OBO,
            // haciendo uso de la sessionKey persistida.
            var res = await ((ILongRunningWebApi)_confidentialClient)
                .AcquireTokenInLongRunningProcess(_scopes, session.Value.SessionKeyPlain)
                .ExecuteAsync();

            // Actualizar metadatos de uso de la sesión (última vez utilizada).
            await _lroSessionRepository.UpdateLastUsedAsync(session.Value.Id, upn);

            // Construcción de la solicitud HTTP hacia Microsoft Graph para obtener el binario del hostedContent.
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://graph.microsoft.com/v1.0/chats/{chatId}/messages/{messageId}/hostedContents/{contentId}/$value"
            );

            // Autenticación mediante Bearer Token obtenido del proceso LRO.
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", res.AccessToken);

            // Solicitud a Graph; ResponseHeadersRead optimiza el manejo del stream.
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return null;

            // Leer los bytes del contenido de la imagen.
            var bytes = await response.Content.ReadAsByteArrayAsync();

            // Determinar el tipo MIME; por defecto se usa image/png.
            var type = response.Content.Headers.ContentType?.MediaType ?? "image/png";

            return new GraphImageResponse
            {
                Content = bytes,
                ContentType = type
            };
        }
        catch (MsalUiRequiredException)
        {
            // Si MSAL indica que se requiere UI es porque la sesión perdió validez
            // (revocación, expiración prolongada, CAE, etc.). Se marca la sesión como inactiva.
            await _lroSessionRepository.DeactivateAllByUserObjectIdAsync(upn);
            return null;
        }
        catch (Exception ex)
        {
            // Captura genérica para evitar propagación de fallos hacia capas superiores.
            Console.WriteLine($"Error al obtener imagen adjunta: {ex.Message}");
            return null;
        }
    }
}
