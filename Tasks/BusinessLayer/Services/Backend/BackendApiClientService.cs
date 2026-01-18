using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using Academikus.AgenteInteligenteMentoresTareas.Entity.OutPut;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.Backend;

public class BackendApiClientService : IBackendApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseApiUrl;
    private readonly DBContext _context;
    private readonly IConfiguration _configuration;

    public BackendApiClientService(
        HttpClient httpClient,
        IHostEnvironment env,
        DBContext context,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _context = context;

        if (env.IsDevelopment())
        {
            _baseApiUrl = Require(configuration["Backend:Endpoint"], "Backend:Endpoint no configurado");
        }
        else if (env.IsEnvironment("Production") || env.IsStaging())
        {
            var fe = Require(configuration["Frontend:Endpoint"], "Frontend:Endpoint no configurado");
            _baseApiUrl = $"{fe.TrimEnd('/')}/backend";
        }
        else
        {
            throw new InvalidOperationException($"Ambiente no soportado: {env.EnvironmentName}");
        }
    }

    public async Task<bool> SendMessageToChatAsync(string chatId, string htmlContent)
    {
        try
        {
            // Obtener token App-only desde Azure AD
            var token = await GetAppTokenAsync();

            // Construir URL y payload
            var url = $"{_baseApiUrl}/api/chat/{chatId}/message";
            var payload = new
            {
                SenderRole = "Mentor",
                ContentType = "html",
                Content = htmlContent
            };

            var json = JsonConvert.SerializeObject(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            // Agregar encabezado Authorization con el token
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            // Enviar la solicitud
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error al enviar mensaje a API: {response.StatusCode} - {error}");

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción al enviar mensaje a API: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateAISettingsAsync(string chatId, bool aiState, string reason)
    {
        try
        {
            // Obtener token de aplicación (App-Only)
            var token = await GetAppTokenAsync();

            // Construir URL y payload
            var url = $"{_baseApiUrl}/api/chat/{chatId}/ai-settings";
            var payload = new
            {
                AIState = aiState,
                AIChangeReason = reason
            };

            var json = JsonConvert.SerializeObject(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Agregar encabezado Authorization con el token
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Enviar solicitud
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error al actualizar estado de IA: {response.StatusCode} - {error}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción al actualizar estado de IA: {ex.Message}");
            return false;
        }
    }

    public async Task<StudentContextModel?> GetStudentContextByEmailAsync(string email)
    {
        try
        {
            // Obtener token de aplicación (App-Only)
            var token = await GetAppTokenAsync();

            // Construir URL
            var url = $"{_baseApiUrl}/api/student/{email}";

            // Crear la solicitud con encabezado Authorization
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Enviar la solicitud
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error al obtener contexto del estudiante: {response.StatusCode} - {error}");

                return null;
            }

            // Leer el cuerpo de la respuesta y deserializar
            var json = await response.Content.ReadAsStringAsync();
            var dto = JsonConvert.DeserializeObject<WebApiResponseDto<StudentContextModel>>(json);

            return dto.ResponseData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción al obtener contexto del estudiante: {ex.Message}");
            return null;
        }
    }

    public async Task<StudentBannerData?> GetStudentBannerDataAsync(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                Console.WriteLine("Email vacío o nulo al pedir StudentBannerData.");
                return null;
            }

            // Obtener token de aplicación (App-Only)
            var token = await GetAppTokenAsync();

            // Construir URL
            var baseUrl = _baseApiUrl?.TrimEnd('/') ?? "";
            var url = $"{baseUrl}/api/user/student-info/{Uri.EscapeDataString(email)}";

            // Crear la solicitud con encabezado Authorization
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Enviar la solicitud
            var response = await _httpClient.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error Backend API Student: {response.StatusCode} - {json}");
                return null;
            }

            // Deserializa y devuelve solo ResponseData
            var envelope = JsonConvert.DeserializeObject<BackendWebApiResponse<StudentBannerData>>(json);

            if (envelope == null)
            {
                Console.WriteLine("No se pudo deserializar el envelope de StudentInfoResponse.");
                return null;
            }

            if (envelope.ResponseCode != 0)
            {
                Console.WriteLine($"Respuesta no OK. Code={envelope.ResponseCode} Msg={envelope.ResponseMessage}");
                return null;
            }

            if (envelope.ResponseData == null)
            {
                Console.WriteLine("Envelope OK pero ResponseData es nulo.");
                return null;
            }

            return envelope.ResponseData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción al obtener StudentBannerData: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Llama al backend para crear y guardar un resumen.
    /// </summary>
    /// <param name="chatId">MSTeamsChatId</param>
    /// <param name="summaryType">"IA" | "Mentor" | "Bajo Demanda" | "Diario"</param>
    /// <returns>(Ok, Summary, Error)</returns>
    public async Task<(bool ok, DailySummaryApiResponse? summary, string? error)> CreateDailySummaryAsync(
        string chatId)
    {
        try
        {
            // Obtener token App-only desde Azure AD
            var token = await GetAppTokenAsync();

            // Construir URL base
            var baseUrl = _baseApiUrl?.TrimEnd('/') ?? "";
            var url = $"{baseUrl}/api/chat/{chatId}/summary";

            // Construir la solicitud con el cuerpo JSON
            var payload = new { SummaryType = "Diario" };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Agregar encabezado Authorization
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _httpClient.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error al crear resumen diario: {resp.StatusCode} - {body}");
                return (false, null, $"HTTP {(int)resp.StatusCode}: {body}");
            }

            // Deserializar respuesta
            var dto = System.Text.Json.JsonSerializer.Deserialize<DailySummaryApiResponse>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return (true, dto, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción al crear resumen diario: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool ok, GeneralSalesforceUserResponse? mentor, string? error)> GetMentorFromBackendAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, null, "El email es obligatorio.");

        try
        {
            // Obtener token App-only desde Azure AD
            var token = await GetAppTokenAsync();

            // Construir URL
            var url = $"{_baseApiUrl?.TrimEnd('/')}/api/mentor/salesforce/{Uri.EscapeDataString(email)}";

            // Crear la solicitud con encabezado Authorization
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Enviar la solicitud
            using var resp = await _httpClient.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error al obtener mentor desde backend: {resp.StatusCode} - {body}");
                return (false, null, $"HTTP {(int)resp.StatusCode}: {body}");
            }

            // Intentar deserializar la respuesta
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            BackendEnvelope<GeneralSalesforceUserResponse>? envelope;
            try
            {
                envelope = System.Text.Json.JsonSerializer.Deserialize<BackendEnvelope<GeneralSalesforceUserResponse>>(body, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al leer JSON: {ex.Message}");
                return (false, null, $"No se pudo leer el JSON del backend: {ex.Message}");
            }

            if (envelope is null)
                return (false, null, "Respuesta vacía del backend.");

            if (envelope.ResponseCode != 0)
                return (false, null,
                    string.IsNullOrWhiteSpace(envelope.ResponseMessage)
                        ? "El backend devolvió un error."
                        : envelope.ResponseMessage);

            // Validar registros obtenidos
            var total = envelope.ResponseData?.TotalSize ?? 0;
            if (total <= 0 || envelope.ResponseData?.Records is null || envelope.ResponseData.Records.Count == 0)
                return (false, envelope.ResponseData, "No se encontraron registros en Salesforce.");

            return (true, envelope.ResponseData, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción al obtener mentor desde backend: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool ok, CreateCaseResponse? data, string? error)> CreateCaseAsync(
        string bannerStudent,
        string bannerMentor,
        string ownerEmail,
        string summary,
        string theme,
        DateOnly nextDate)
    {
        try
        {
            // Obtiene un token de aplicación (App-Only) desde Azure AD.
            var token = await GetAppTokenAsync();

            // Construye la URL base para la solicitud al backend.
            var baseUrl = _baseApiUrl?.TrimEnd('/') ?? "";
            var url = $"{baseUrl}/api/case";

            // Crea el objeto que se enviará en el cuerpo de la solicitud.
            var payload = new CreateCaseRequest(
                BannerStudent: bannerStudent,
                BannerMentor: bannerMentor,
                OwnerEmail: ownerEmail,
                Summary: summary,
                Theme: theme,
                NextDate: nextDate);

            // Crea la solicitud HTTP POST con el contenido JSON.
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };

            // Agrega el encabezado Authorization con el token Bearer.
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Envía la solicitud al backend.
            using var resp = await _httpClient.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            // Valida si la respuesta fue exitosa.
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error al crear el caso: {(int)resp.StatusCode} - {resp.ReasonPhrase}");
                Console.WriteLine(json);
                return (false, null, $"[{(int)resp.StatusCode}] {resp.ReasonPhrase} | {json}");
            }

            // Intenta deserializar la respuesta del backend.
            var parsed = System.Text.Json.JsonSerializer.Deserialize<BackendWebApiResponse<CreateCaseResponse>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Valida que la respuesta no sea nula.
            if (parsed is null)
            {
                Console.WriteLine("Error: respuesta nula del backend.");
                return (false, null, "Respuesta nula del backend.");
            }

            // Verifica si el backend devolvió un código de error.
            if (parsed.ResponseCode != ResponseTypeCodeDto.Ok)
            {
                Console.WriteLine($"Error al crear caso: {parsed.ResponseMessage} (code={(int)parsed.ResponseCode})");
                return (false, null, $"{parsed.ResponseMessage ?? "Error"} (code={(int)parsed.ResponseCode})");
            }

            // El caso se creó correctamente, imprime información de confirmación.
            Console.WriteLine($"Caso creado correctamente para el estudiante {bannerStudent}.");
            return (true, parsed.ResponseData, null);
        }
        catch (Exception ex)
        {
            // Captura cualquier excepción inesperada durante el proceso.
            Console.WriteLine($"Excepción al crear el caso: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string htmlBody, string fileUrl)
    {
        try
        {
            // Obtiene un token App-Only desde Azure AD para autenticar la solicitud.
            var token = await GetAppTokenAsync();

            using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Descarga el archivo desde la URL especificada.
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error al obtener archivo: {response.StatusCode}");
                return false;
            }

            var fileBytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            // Convierte el archivo descargado a Base64.
            var base64 = Convert.ToBase64String(fileBytes);

            var contentDisposition = response.Content.Headers.ContentDisposition;
            string fileName = await _context.MessageAttachments
                                .Where(a => a.InternalContentUrl == fileUrl)
                                .Select(a => a.FileName)
                                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(fileName))
            {
                var itemId = Path.GetFileName(new Uri(fileUrl).AbsolutePath);
                fileName = await _context.MessageAttachments
                    .Where(a => a.ItemId == itemId)
                    .Select(a => a.FileName)
                    .FirstOrDefaultAsync();
            }

            // Si no hay nombre, genera uno por defecto.
            if (string.IsNullOrEmpty(fileName))
                fileName = $"adjunto_{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Construye el DTO de adjunto.
            var attachment = new Base64AttachmentDto
            {
                FileName = fileName,
                ContentType = contentType,
                Base64 = base64
            };

            // Construye el objeto de solicitud del correo.
            var emailRequest = new SendEmailJsonRequest
            {
                To = to,
                Subject = subject,
                HtmlBody = htmlBody,
                Attachment = attachment 
            };

            // Serializa el cuerpo de la solicitud a JSON.
            var json = System.Text.Json.JsonSerializer.Serialize(emailRequest);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Crea la solicitud POST al endpoint interno de correos.
            var emailEndpoint = $"{_baseApiUrl}/api/email/send";
            using var req = new HttpRequestMessage(HttpMethod.Post, emailEndpoint)
            {
                Content = stringContent
            };

            // Agrega el encabezado Authorization con el token Bearer.
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Envía la solicitud al backend.
            var emailResponse = await _httpClient.SendAsync(req);
            var responseBody = await emailResponse.Content.ReadAsStringAsync();

            try
            {
                var apiResult = System.Text.Json.JsonSerializer.Deserialize<WebApiResponseDTO>(responseBody);

                if (apiResult == null)
                {
                    Console.WriteLine("Error: No se pudo deserializar la respuesta del backend.");
                    return false;
                }

                if (apiResult.ResponseCode != 0)
                {
                    Console.WriteLine($"Error enviando email (API): {apiResult.ResponseMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando respuesta JSON: {ex.Message}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Registra cualquier excepción ocurrida durante el proceso.
            Console.WriteLine($"Excepción al enviar correo: {ex.Message}");
            return false;
        }
    }

    public async Task<string> GetAppTokenAsync()
    {
        var tenantId = _configuration["AzureAd:TenantId"];
        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];
        var scope = _configuration["AzureAd:ApiScope"];

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { scope })
        );

        return token.Token;
    }

    private static string Require(string? value, string error)
        => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentNullException(error);
}
