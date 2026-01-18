using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.TokenProvider;

/// <summary>
/// Proveedor encargado de obtener y administrar el token OAuth de Salesforce,
/// utilizando credenciales configuradas en <see cref="SalesforceOptions"/>.
/// 
/// Implementa un sistema de caché interno para evitar solicitudes repetidas
/// al endpoint de autenticación de Salesforce, mejorando el rendimiento
/// y reduciendo la carga en el servicio externo.
/// </summary>
public sealed class SalesforceTokenProvider : ISalesforceTokenProvider
{
    private const string CacheKey = "salesforce_access_token";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly SalesforceOptions _saleforceOptions;

    /// <summary>
    /// Inicializa una nueva instancia del proveedor de tokens de Salesforce.
    /// </summary>
    /// <param name="httpClientFactory">Factoría de clientes HTTP configurados para Salesforce.</param>
    /// <param name="cache">Caché en memoria utilizada para almacenar el token temporalmente.</param>
    /// <param name="options">Opciones de configuración necesarias para autenticar contra Salesforce.</param>
    public SalesforceTokenProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<SalesforceOptions> options
        )
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _saleforceOptions = options.Value;
    }

    /// <summary>
    /// Obtiene un token OAuth válido para Salesforce.
    /// 
    /// Si existe un token previamente almacenado en caché, se devuelve directamente.
    /// De lo contrario, se realiza una solicitud al endpoint de autenticación
    /// de Salesforce usando el flujo "username + password + security token".
    /// </summary>
    /// <param name="ct">Token de cancelación para finalizar la operación si es necesario.</param>
    /// <returns>Un string que contiene el token de acceso (access_token).</returns>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si la configuración es inválida, si Salesforce devuelve un error
    /// o si la respuesta no contiene un token válido.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Se lanza ante fallas de red, tiempo de espera o imposibilidad de conectar con Salesforce.
    /// </exception>
    /// <exception cref="TaskCanceledException">
    /// Se lanza si la operación fue cancelada mediante el parámetro <paramref name="ct"/>.
    /// </exception>
    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        try
        {
            // Verifica si ya hay un token válido en caché
            if (_cache.TryGetValue<string>(CacheKey, out var cached) && !string.IsNullOrWhiteSpace(cached))
            {
                Console.WriteLine("[INFO] Token de Salesforce recuperado desde caché.");
                return cached!;
            }

            // Valida configuraciones mínimas requeridas para OAuth2
            if (string.IsNullOrWhiteSpace(_saleforceOptions.GrantType))
                throw new InvalidOperationException("Config Salesforce:GrantType vacía.");
            if (string.IsNullOrWhiteSpace(_saleforceOptions.ClientId))
                throw new InvalidOperationException("Config Salesforce:ClientId vacía.");
            if (string.IsNullOrWhiteSpace(_saleforceOptions.ClientSecret))
                throw new InvalidOperationException("Config Salesforce:ClientSecret vacía.");
            if (string.IsNullOrWhiteSpace(_saleforceOptions.UserName))
                throw new InvalidOperationException("Config Salesforce:UserName vacía.");
            if (string.IsNullOrWhiteSpace(_saleforceOptions.Password) && string.IsNullOrWhiteSpace(_saleforceOptions.TokenSecret))
                throw new InvalidOperationException("Config Salesforce:Password y/o TokenSecret vacíos.");

            // Combina contraseña + token secreto
            var passwordPlusToken = string.Concat(_saleforceOptions.Password ?? string.Empty, _saleforceOptions.TokenSecret ?? string.Empty);

            // Crea cliente HTTP configurado para Salesforce OAuth
            var client = _httpClientFactory.CreateClient("salesforce-oauth");
            client.BaseAddress = new Uri(_saleforceOptions.LoginBaseUrl);

            // Construye el cuerpo de la solicitud (application/x-www-form-urlencoded)
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = _saleforceOptions.GrantType,
                ["client_id"] = _saleforceOptions.ClientId,
                ["client_secret"] = _saleforceOptions.ClientSecret,
                ["username"] = _saleforceOptions.UserName,
                ["password"] = passwordPlusToken
            };

            using var content = new FormUrlEncodedContent(form);

            // Determina endpoint final de autenticación
            var tokenPath = string.IsNullOrWhiteSpace(_saleforceOptions.RequestUri?.Token)
                ? "/services/oauth2/token"
                : _saleforceOptions.RequestUri.Token;

            // Ejecuta solicitud POST hacia Salesforce
            using var response = await client.PostAsync(tokenPath, content, ct);

            // Lee la respuesta completa como texto
            var raw = await response.Content.ReadAsStringAsync(ct);

            // Valida código de estado HTTP
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Error al obtener token de Salesforce. Status {(int)response.StatusCode}: {raw}");

            // Deserializa respuesta JSON
            var tokenResponse = JsonConvert.DeserializeObject<SalesforceSecurityTokenResponse>(raw)
                            ?? throw new InvalidOperationException($"Respuesta de token inválida: {raw}");

            // Valida contenido del token
            if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
                throw new InvalidOperationException($"No se recibió access_token. Respuesta: {raw}");

            // Cachea token con TTL conservador (50 min)
            _cache.Set(CacheKey, tokenResponse.AccessToken, TimeSpan.FromMinutes(50));

            return tokenResponse.AccessToken;
        }
        catch (HttpRequestException ex)
        {
            // Error de red o de conexión
            Console.WriteLine($"[SalesforceTokenProvider][HTTP ERROR] Error al obtener token de Salesforce: {ex.Message}");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            // Configuración o respuesta inválida
            Console.WriteLine($"[SalesforceTokenProvider][CONFIG ERROR] Error de configuración o respuesta inválida en Salesforce: {ex.Message}");
            throw;
        }
        catch (TaskCanceledException ex) when (ct.IsCancellationRequested)
        {
            // Cancelación explícita de la operación
            Console.WriteLine($"[SalesforceTokenProvider][CANCELLED] Solicitud de token cancelada por el usuario: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            // Error inesperado
            Console.WriteLine($"[SalesforceTokenProvider][UNEXPECTED ERROR] Error inesperado en GetTokenAsync: {ex}");
            throw;
        }
    }
}
