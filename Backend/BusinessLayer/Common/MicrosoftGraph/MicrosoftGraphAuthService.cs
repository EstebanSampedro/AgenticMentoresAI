using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.MicrosoftGraph;

/// <summary>
/// Servicio encargado de obtener y almacenar temporalmente un token de acceso para
/// Microsoft Graph usando autenticación App-Only (Client Credentials).
/// Maneja caché en memoria para evitar solicitudes repetidas.
/// </summary>
public class MicrosoftGraphAuthService
{
    private readonly AzureAdOptions _graphOptions;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Token actual en memoria y su fecha de expiración
    private string? _accessToken;
    private DateTime _tokenExpiration = DateTime.MinValue;

    /// <summary>
    /// Inicializa el servicio con configuración de Azure AD para Graph.
    /// </summary>
    /// <param name="graphOptions">
    /// Valores configurados de AzureAdOptions desde appsettings.json o Key Vault.
    /// </param>
    public MicrosoftGraphAuthService(IOptions<AzureAdOptions> graphOptions)
    {
        _graphOptions = graphOptions?.Value
            ?? throw new ArgumentNullException(nameof(graphOptions));
    }

    /// <summary>
    /// Obtiene un token de acceso válido para realizar llamadas a Microsoft Graph.
    /// Utiliza caché en memoria para optimizar solicitudes y evitar throttling.
    /// </summary>
    /// <returns>Token de acceso como cadena.</returns>
    public async Task<string> GetAccessTokenAsync()
    {
        // Retorna el token en caché si sigue vigente
        if (!string.IsNullOrEmpty(_accessToken) && 
            DateTime.UtcNow < _tokenExpiration)
        {
            return _accessToken;
        }

        await _semaphore.WaitAsync();
        try
        {
            // Segunda verificación por posibles condiciones de carrera
            if (!string.IsNullOrEmpty(_accessToken) && 
                DateTime.UtcNow < _tokenExpiration)
            {
                return _accessToken;
            }

            // Validación de configuración obligatoria
            if (string.IsNullOrWhiteSpace(_graphOptions.TenantId) ||
                string.IsNullOrWhiteSpace(_graphOptions.ClientId) ||
                string.IsNullOrWhiteSpace(_graphOptions.ClientSecret))
            {
                Console.WriteLine("[GraphAuth] Falta configuración de AzureAdOptions");
                throw new InvalidOperationException("Configuración incompleta para Azure AD.");
            }

            // Construcción del cliente usando Client Credentials Flow
            var app = ConfidentialClientApplicationBuilder
                .Create(_graphOptions.ClientId)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{_graphOptions.TenantId}"))
                .WithClientSecret(_graphOptions.ClientSecret)
                .Build();

            // Solicitud del token para el scope de Graph .default
            var authResult = await app
                .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
                .ExecuteAsync();

            // Almacena en memoria y refresca antes de expirar
            _accessToken = authResult.AccessToken;
            _tokenExpiration = authResult.ExpiresOn.UtcDateTime.AddMinutes(-5);

            return _accessToken;
        }
        catch (MsalServiceException ex)
        {
            Console.WriteLine($"[GraphAuth] Error de servicio en Azure AD: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphAuth] Error al generar token: {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
