using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static Microsoft.Graph.Constants;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.BannerWebApi;

public class BannerWebApiService : IBannerWebApiService
{
    private readonly HttpClient _httpClient;
    private readonly BannerWebApiOptions _bannerWebApiOptions;

    private BannerWebApiTokenResponse? _cachedToken;
    private DateTime _tokenExpiration;

    public BannerWebApiService(
        HttpClient httpClientFactory,
        IOptions<BannerWebApiOptions> bannerWebApiOptions,
        IConfiguration configuration)
    {
        _httpClient = httpClientFactory;
        _bannerWebApiOptions = bannerWebApiOptions.Value ?? throw new ArgumentNullException(nameof(bannerWebApiOptions));

        if (string.IsNullOrWhiteSpace(bannerWebApiOptions.Value.BaseUrl))
            throw new InvalidOperationException("La configuración BannerWebApi:BaseUrl está vacía o no definida en appsettings.json.");

        // Configuración de la URL base desde las opciones
        _httpClient.BaseAddress = new Uri(_bannerWebApiOptions.BaseUrl);
    }

    /// <summary>
    /// Obtiene un token de acceso válido desde Banner Web API.
    /// Utiliza caché en memoria y refresca el token antes de expirar.
    /// </summary>
    /// <returns>Token deserializado o null si falla.</returns>
    private async Task<BannerWebApiTokenResponse?> GetTokenAsync()
    {
        try
        {
            // Reutiliza token válido en caché para evitar throttling
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiration)
                return _cachedToken;

            // Asegura que el header Accept esté configurado para JSON
            if (!_httpClient.DefaultRequestHeaders.Accept.Any())
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

            // Normaliza credenciales configuradas
            var grantType = (_bannerWebApiOptions.GrantType ?? "").Trim();
            var username = (_bannerWebApiOptions.Username ?? "").Trim();
            var password = (_bannerWebApiOptions.Password ?? "").Trim();

            // Construcción del cuerpo tipo x-www-form-urlencoded
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = grantType,
                ["password"] = password,
                ["username"] = username
            };

            using var content = new FormUrlEncodedContent(form);

            // Llamada al endpoint de autenticación
            using var response = await _httpClient
                .PostAsync("token", content)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[BannerWebApi][ERROR] Status={response.StatusCode}, Body={body}");
                return null;
            }

            var token = JsonSerializer.Deserialize<BannerWebApiTokenResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0)
            {
                Console.WriteLine($"[BannerWebApi][ERROR] Respuesta inválida: {body}");
                return null;
            }

            _cachedToken = token;
            _tokenExpiration = DateTime.UtcNow.AddSeconds(Math.Max(10, token.ExpiresIn - 10));

            return _cachedToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BannerWebApi][EXCEPTION] Error al obtener token: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Inicia el flujo de justificación de inasistencia en Banner Web API.
    /// </summary>
    public async Task<StudentJustificationResponse?> StartStudentJustificationFlowAsync(
        StudentJustificationRequest request)
    {
        try
        {
            // Validaciones de request
            if (request is null)
            {
                Console.WriteLine("[BannerWebApi] DTO nulo.");
                return new StudentJustificationResponse
                {
                    ResponseCode = 2,
                    ResponseMessage = "DTO nulo."
                };
            }

            var token = await GetTokenAsync();

            if (token == null)
            {
                Console.WriteLine("[BannerWebApi][ERROR] No se pudo obtener token para Banner.");
                return new StudentJustificationResponse
                {
                    ResponseCode = 2,
                    ResponseMessage = "No se pudo obtener token."
                };
            }

            _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient
                .PostAsync("api/StudentJustificationRequest", content)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[BannerWebApi][ERROR] Status={response.StatusCode}, Body={body}");

                return new StudentJustificationResponse
                {
                    ResponseCode = 2,
                    ResponseMessage = $"HTTP error {(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }

            StudentJustificationResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<StudentJustificationResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException jex)
            {
                Console.WriteLine($"[BannerWebApi][ERROR] JsonException={jex.Message}");

                return new StudentJustificationResponse
                {
                    ResponseCode = 2,
                    ResponseMessage = $"JSON inválido: {jex.Message}"
                };
            }

            if (parsed?.ResponseCode != 0)
            {
                Console.WriteLine($"[BannerWebApi][WARN] Flujo con código={parsed?.ResponseCode}, Msg={parsed?.ResponseMessage}");
            }

            return parsed;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BannerWebApi][EXCEPTION] Justification: {ex}");
            return new StudentJustificationResponse
            {
                ResponseCode = 2,
                ResponseMessage = $"Excepción: {ex.Message}"
            };
        }
    }
}
