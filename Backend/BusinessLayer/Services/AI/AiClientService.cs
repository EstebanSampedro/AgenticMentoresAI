using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models.CallRecords;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.AI;

/// <summary>
/// Cliente de integración con los servicios de IA que generan resúmenes u otras operaciones
/// externas basadas en texto proveniente de mensajes de chat.
/// </summary>
public class AiClientService : IaiClientService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AgentOptions _agentOptions;
        
    /// <summary>
    /// Inicializa la instancia del cliente para comunicación con la IA.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP configurado por DI.</param>
    /// <param name="agentOptions">Opciones del agente IA.</param>
    /// <param name="tokenService">Servicio para generar tokens que autorizan el consumo de la IA.</param>
    public AiClientService(
        HttpClient httpClient,
        IConfiguration configuration,
        IOptions<AgentOptions> agentOptions)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _agentOptions = agentOptions.Value;        
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
    /// Llama al endpoint externo encargado de generar un resumen IA para una conversación dada.
    /// </summary>
    /// <param name="chatId">Identificador del chat asociado al resumen.</param>
    /// <param name="conversation">Texto que se enviará a IA para generar el resumen.</param>
    /// <returns>Respuesta de IA deserializada o null si hay fallos.</returns>
    public async Task<SummaryApiResponse?> CallSummaryAgentAsync(
        string chatId, 
        string conversation)
    {
        // Validación básica de parámetros
        //if (string.IsNullOrWhiteSpace(chatId))
        //{
        //    Console.WriteLine("[AI Summary] chatId inválido.");
        //    return null;
        //}

        //if (string.IsNullOrWhiteSpace(conversation))
        //{
        //    Console.WriteLine("[AI Summary] La conversación está vacía o es nula.");
        //    return null;
        //}

        try
        {
            // Obtener token de autenticación
            var accessToken = await GenerateAccessTokenAsync();

            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("[AI Summary] No se pudo obtener el token para llamar al endpoint de resumen.");
                return null;
            }

            // Construcción de URL usando parámetros
            var endpointUrl = $"{_agentOptions.Endpoint}/api/v1/summary/summary/";

            var payload = new
            {
                session_id = chatId,
                conversation = conversation
            };

            // Cuerpo JSON vacío requerido por el endpoint
            var payloadJson = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
            {
                Content = content
            };

            // Token Bearer
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Enviar solicitud
            using var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Respuesta resumen IA: {(int)response.StatusCode}");
            Console.WriteLine(json);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Resumen API respondió {(int)response.StatusCode}: {json}");

                return null;
            }

            // CONFIGURACIÓN EXPLÍCITA DE DESERIALIZACIÓN
            // Esto evita que configuraciones globales del proyecto afecten este llamado específico
            var settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset, // Asegura lectura correcta del ISO 8601
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore // Evita error si la API agrega campos nuevos
            };

            var parsed = JsonConvert.DeserializeObject<SummaryApiResponse>(json, settings);

            return parsed;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[AI Summary] Error HTTP para chat {chatId}: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[AI Summary] Timeout para chat {chatId}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AI Summary] Error inesperado para chat {chatId}: {ex}");
            return null;
        }
    }
}
