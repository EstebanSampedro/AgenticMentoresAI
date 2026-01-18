using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.BannerWebApi;

/// <summary>
/// Servicio encargado de comunicarse con Banner Web API para obtener
/// información académica y generar tokens en base a credenciales configuradas.
/// </summary>
public class BannerWebApiService : IBannerWebApiService
{
    private readonly HttpClient _httpClient;
    private readonly BannerWebApiOptions _bannerWebApiOptions;

    private BannerWebApiTokenResponse? _cachedToken;
    private DateTime _tokenExpiration;

    /// <summary>
    /// Inicializa el servicio con dependencias inyectadas.
    /// </summary>
    public BannerWebApiService(
        IHttpClientFactory httpClientFactory,
        IOptions<BannerWebApiOptions> bannerWebApiOptions)
    {
        _httpClient = httpClientFactory.CreateClient();
        _bannerWebApiOptions = bannerWebApiOptions.Value ?? throw new ArgumentNullException(nameof(bannerWebApiOptions));

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
    /// Obtiene información académica y administrativa básica según el correo institucional.
    /// </summary>
    public async Task<BasicInformationResponse?> GetBasicInformationAsync(BasicInformationRequest request)
    {
        try
        {
            if (request is null || string.IsNullOrWhiteSpace(request.InstitutionalEmail))
            {
                Console.WriteLine("[BannerWebApi][WARN] Solicitud inválida en BasicInformation.");
                return null;
            }

            var token = await GetTokenAsync();

            if (token == null)
            {
                Console.WriteLine("[BannerWebApi][ERROR] No se pudo obtener token para consultar API.");
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.AccessToken);

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient
                    .PostAsync("api/BasicInformation", content)
                    .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"[BannerWebApi][ERROR] Status={response.StatusCode}, Body={body}");
                return null;
            }

            var basicInfo = JsonSerializer.Deserialize<BasicInformationResponse>(
                    body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return basicInfo;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BannerWebApi][EXCEPTION] Error BasicInformation: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Obtiene identificadores clave del estudiante y lista de sus programas activos.
    /// </summary>
    public async Task<(
        string PersonId, 
        string BannerId, 
        string Pidm, 
        List<string> ProgramDescriptions)> 
        GetStudentKeyInfoAsync(string institutionalEmail)
    {
        if (string.IsNullOrWhiteSpace(institutionalEmail))
        {
            Console.WriteLine("[BannerWebApi][WARN] Email vacío en GetStudentKeyInfoAsync.");
            return (string.Empty, string.Empty, string.Empty, new List<string>());
        }

        var response = await GetBasicInformationAsync(
                new BasicInformationRequest { InstitutionalEmail = institutionalEmail })
                .ConfigureAwait(false);

        if (response?.Content != null && response.ResponseCode == 0)
        {
            var programs = response.Content.Student?.StudentCareers?
                .Select(c => c.ProgramDesc)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList() ?? new List<string>();

            return (
                response.Content.PersonId,
                response.Content.BannerId,
                response.Content.Pidm,
                programs
            );
        }

        Console.WriteLine("[BannerWebApi][INFO] Estudiante no encontrado o sin datos válidos.");
        return (string.Empty, string.Empty, string.Empty, new List<string>());
    }
}
