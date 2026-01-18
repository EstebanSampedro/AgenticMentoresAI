using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.AI;

public class AiClientService : IaiClientService
{
    private readonly HttpClient _httpClient;
    private readonly IBackendApiClientService _backendService;
    private readonly AgentOptions _agentOptions;
    private readonly IConfiguration _configuration;

    public AiClientService(
        HttpClient httpClient,
        IBackendApiClientService backendService,
        IOptions<AgentOptions> agentOptions,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _backendService = backendService;
        _agentOptions = agentOptions.Value;
        _configuration = configuration;
    }

    /// <summary>
    /// Genera un token válido utilizando Client Credentials Flow contra Azure AD.
    /// </summary>
    /// <returns>Un token de acceso como cadena.</returns>
    public async Task<string> GenerateAccessTokenAsync()
    {
        // Obtención segura de configuración requerida
        var tenantId = _configuration["AiSecurity:TenantId"]?.Trim();
        var clientId = _configuration["AiSecurity:ClientId"]?.Trim();
        var clientSecret = _configuration["AiSecurity:ClientSecret"]?.Trim();
        var scope = _configuration["AiSecurity:Scope"]?.Trim();

        // Validaciones explícitas
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            string.IsNullOrWhiteSpace(scope))
        {
            Console.WriteLine("[TokenGenerator] Configuración AiSecurity incompleta.");
            return string.Empty;
        }

        try
        {
            var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";

            // Construcción del cliente para Client Credentials Flow
            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(authority)
                .Build();

            // Solicitud del token
            var result = await app
                .AcquireTokenForClient(new[] { scope })
                .ExecuteAsync()
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.AccessToken))
            {
                Console.WriteLine("[TokenGenerator][ERROR] Token vacío recibido desde Azure AD.");
                return string.Empty;
            }

            return result.AccessToken;
        }
        catch (MsalServiceException ex)
        {
            Console.WriteLine($"[TokenGenerator][MSAL] Error de servicio al generar token: {ex.Message}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TokenGenerator][EXCEPTION] Error inesperado al generar token: {ex}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Llama al agente de texto enviando los parámetros como JSON en el cuerpo.
    /// </summary>
    public async Task<AgentResponse?> CallTextAgentAsync(AgentRequest request)
    {
        try
        {
            // Validación básica del modelo
            if (request is null)
            {
                Console.WriteLine("[Agent] Request nulo.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                Console.WriteLine("[Agent] Campos requeridos faltantes en AgentRequest.");
                return null;
            }

            // Obtener token
            var token = await GenerateAccessTokenAsync()
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("[Agent] No se pudo obtener token");
                return null;
            }

            // Construir URL del agente
            var url = $"{_agentOptions.Endpoint}/api/v1/agents/agent/";

            // Serializar el request como JSON
            var json = JsonSerializer.Serialize(request);
            using var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            httpRequest.Content = jsonContent;

            // Enviar al agente IA
            using var response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Agent][ERROR] {response.StatusCode}: {body}");
                return null;
            }

            return JsonSerializer.Deserialize<AgentResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Agent][EXCEPTION] {ex}");
            return null;
        }
    }

    /// <summary>
    /// Envía una lista de URLs de imágenes a la IA para análisis
    /// y devuelve las respuestas obtenidas.
    /// </summary>
    public async Task<List<ImageAnalysisResponse>> CallImagesAgentAsync(
        List<string> imageUrls, 
        string chatId)
    {
        var responses = new List<ImageAnalysisResponse>();

        // Validar que existan imágenes para procesar
        if (imageUrls == null || imageUrls.Count == 0)
        {
            Console.WriteLine("[IA Image] Lista de imágenes vacía.");
            return responses;
        }

        // Validar chatId como contexto de sesión
        if (string.IsNullOrWhiteSpace(chatId))
        {
            Console.WriteLine("[IA Image] chatId inválido.");
            return responses;
        }

        // Token delegado para autenticarse con la IA
        var aiAccessToken = await GenerateAccessTokenAsync()
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(aiAccessToken))
        {
            Console.WriteLine("[IA Image] No se obtuvo token para IA.");
            return responses;
        }

        // Token del backend requerido para recuperar la imagen desde la URL protegida
        var backAccessToken = await _backendService.GetAppTokenAsync()
            .ConfigureAwait(false);

        foreach (var imageUrl in imageUrls)
        {
            Console.WriteLine($"[IA Image] {imageUrl}");

            try
            {
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    Console.WriteLine("[IA Image] URL de imagen inválida.");
                    continue;
                }

                // Construcción del request para recuperar la imagen desde backend
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                getRequest.Headers.Authorization = 
                    new AuthenticationHeaderValue("Bearer", backAccessToken);

                var imageResponse = await _httpClient
                    .SendAsync(getRequest)
                    .ConfigureAwait(false);

                // Validar que la imagen se haya obtenido con éxito
                if (!imageResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[IA Image] Error obteniendo imagen: {imageResponse.StatusCode}");
                    continue;
                }

                // Obtener bytes y tipo MIME de la imagen
                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var contentType = imageResponse.Content.Headers.ContentType?.MediaType ?? "image/png";

                // Construcción del cuerpo multipart para enviar a IA
                using var form = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(imageBytes);

                // Adjuntar los bytes de la imagen con su respectivo Content-Type
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                form.Add(imageContent, "image_file", $"image.{GetFileExtension(contentType)}");

                // Incluir el identificador del chat como campo adicional requerido por IA
                form.Add(new StringContent(chatId), "session_id");

                // Construir la solicitud POST a IA
                var endpointUrl = $"{_agentOptions.Endpoint}/api/v1/analizeimages/analyze-file/";

                using var postRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                postRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", aiAccessToken);
                postRequest.Content = form;

                // Enviar imagen y esperar respuesta
                var result = await _httpClient
                    .SendAsync(postRequest, HttpCompletionOption.ResponseContentRead)
                    .ConfigureAwait(false);

                var raw = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Validar respuesta HTTP de IA
                if (!result.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[IA Image][ERROR] {result.StatusCode}: {raw}");
                    continue;
                }

                // Intentar deserializar el JSON de la IA
                var iaResponse = JsonSerializer.Deserialize<ImageAnalysisResponse>(
                    raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (iaResponse != null)
                    responses.Add(iaResponse);
            }
            catch (TaskCanceledException)
            {
                // Diferenciar entre timeout y cancelación voluntaria
                Console.WriteLine($"[IA Image] Timeout en {imageUrl}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[IA Image] Error de conexión en {imageUrl}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IA Image][EXCEPTION] {imageUrl}: {ex}");
            }
        }

        return responses;
    }

    /// <summary>
    /// Solicita a la IA el resumen de una conversación activa, enviando el identificador del chat
    /// como parte del cuerpo JSON de la solicitud.
    /// </summary>
    public async Task<SummaryApiResponse?> CallSummaryAgentAsync(string chatId)
    {
        try
        {
            // Validación mínima
            if (string.IsNullOrWhiteSpace(chatId))
            {
                Console.WriteLine("[Summary] chatId inválido.");
                return null;
            }

            // Token delegado para conexión a la IA
            var accessToken = await GenerateAccessTokenAsync()
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                Console.WriteLine("[Summary] No se pudo obtener token de acceso.");
                return null;
            }

            // Endpoint unificado sin query params
            var url = $"{_agentOptions.Endpoint}/api/v1/summary/summary/";

            // Cuerpo JSON requerido por la IA
            var payload = new
            {
                session_id = chatId
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            // Enviar solicitud a IA
            using var resp = await _httpClient.SendAsync(request)
                .ConfigureAwait(false);

            var raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Summary][ERROR] {resp.StatusCode}: {raw}");
                return null;
            }

            return JsonSerializer.Deserialize<SummaryApiResponse>(
                raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[Summary] Timeout chat {chatId}: {ex.Message}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[Summary] Error de conexión chat {chatId}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Summary][EXCEPTION] chat {chatId}: {ex}");
            return null;
        }
    }

    private static string GetFileExtension(string contentType)
    {
        return contentType switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/gif" => "gif",
            _ => "bin"
        };
    }
}
