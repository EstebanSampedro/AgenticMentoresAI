using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.ApiClient;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.TokenProvider;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Semester;

/// <summary>
/// Servicio encargado de consultar información del semestre académico actual
/// directamente desde Salesforce.  
/// Provee métodos para obtener el semestre completo o únicamente su código Banner.
/// </summary>
public class SemesterService : ISemesterService
{
    private readonly ISalesforceApiClient _salesforceApiClient;
    private readonly ISalesforceTokenProvider _salesforceTokenProvider;
    private readonly IConfiguration _configuration;

    public SemesterService(
        ISalesforceApiClient salesforceApiClient,
        ISalesforceTokenProvider salesforceTokenProvider,
        IConfiguration configuration)
    {
        _salesforceApiClient = salesforceApiClient;
        _salesforceTokenProvider = salesforceTokenProvider;
        _configuration = configuration;
    }

    /// <summary>
    /// Obtiene el código del semestre académico actual.
    /// </summary>
    /// <returns>
    /// El código Banner del semestre actual (formato <c>AAAAMM</c>),
    /// o el nombre del semestre si no existe código Banner,
    /// o <c>null</c> si no se encontró información válida.
    /// </returns>
    /// <remarks>
    /// Este método encapsula lógica de fallback para asegurar que siempre
    /// se devuelva algún identificador del semestre cuando sea posible.
    /// </remarks>
    public async Task<string?> GetCurrentSemesterCodeAsync()
    {
        try
        {
            // Invoca método que obtiene el semestre actual desde Salesforce
            var semester = await GetCurrentSemesterAsync();

            // Si no se encontró semestre, retornar null directamente
            if (semester == null)
            {
                Console.WriteLine("[Semester][INFO] No se encontró información del semestre actual.");
                return null;
            }

            // Preferir el código Banner, si no está disponible usar el nombre
            return semester?.BannerCode ?? semester?.Name;
        }
        catch (HttpRequestException ex)
        {
            // Error de comunicación con el servicio remoto
            Console.WriteLine($"[Semester][HTTP ERROR] Error al obtener el semestre actual: {ex.Message}");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            // Error de configuración o acceso inválido
            Console.WriteLine($"[Semester][CONFIG ERROR] Error de configuración al consultar el semestre actual: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Error inesperado
            Console.WriteLine($"[Semester][UNEXPECTED ERROR] Error inesperado en GetCurrentSemesterCodeAsync: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Obtiene la información completa del semestre académico actual desde Salesforce.
    /// </summary>
    /// <returns>
    /// Un objeto <see cref="SemesterInfo"/> que contiene detalles como:
    /// nombre, código Banner, línea de negocio y rango de fechas,
    /// o <c>null</c> si no se encontró un semestre válido.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando la configuración de Salesforce es inválida o incompleta.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Se produce cuando Salesforce devuelve una respuesta inválida.
    /// </exception>
    public async Task<SemesterInfo?> GetCurrentSemesterAsync()
    {
        try
        {
            // Carga configuración base de Salesforce
            var baseUrl = _configuration["Salesforce:BaseUrl"]
                ?? throw new InvalidOperationException("Salesforce:BaseUrl no configurado.");

            var apiBase = _configuration["Salesforce:RequestUri:ServiceVersion"]
                ?? throw new InvalidOperationException("Salesforce:RequestUri:ServiceVersion no configurado.");
            apiBase = apiBase.TrimEnd('/');

            // Lee la consulta SOQL desde el archivo de configuración
            var soql = _configuration["Salesforce:Querys:GetCurrentSemester"]
            ?? throw new InvalidOperationException("Salesforce:Querys:GetCurrentSemester no configurado en appsettings.");

            // Codifica la consulta y construye el endpoint completo
            var encoded = Uri.EscapeDataString(soql);
            var queryEndpoint = $"{apiBase}/query?q={encoded}";

            // Obtiene el token de acceso
            var accessToken = await _salesforceTokenProvider.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("No se obtuvo access token de Salesforce.");

            // Ejecuta la solicitud GET hacia Salesforce
            var rawJson = await _salesforceApiClient.GetAsync(queryEndpoint, accessToken);

            // Deserializa la respuesta JSON
            var result = JsonConvert.DeserializeObject<SalesforceQuery<SalesforceTerm>>(rawJson ?? string.Empty);
            if (result is null)
                throw new HttpRequestException("Respuesta inválida al consultar semestre actual en Salesforce.");

            // Obtiene el primer registro (semestre actual)
            var term = result.Records?.FirstOrDefault();

            // Valida que existan datos suficientes
            if (term is null || term.StartDate is null || term.EndDate is null)
            {
                Console.WriteLine("[Semester][INFO] No se encontró un semestre válido en Salesforce.");
                return null;
            }

            // Construye el objeto de dominio con los datos relevantes
            return new SemesterInfo(
                Name: term.Name,
                BannerCode: term.BannerCode,
                BusinessLine: term.BusinessLine,
                StartDate: term.StartDate.Value,
                EndDate: term.EndDate.Value
            );
        }
        catch (HttpRequestException ex)
        {
            // Error de red o respuesta inválida
            Console.WriteLine($"[Semester][HTTP ERROR] Error al consultar semestre actual en Salesforce: {ex.Message}");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            // Configuración o token no disponible
            Console.WriteLine($"[Semester][CONFIG ERROR] Configuración de Salesforce incompleta o inválida: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Error inesperado no categorizado
            Console.WriteLine($"[Semester][UNEXPECTED ERROR] Error inesperado en GetCurrentSemesterAsync: {ex}");
            return null;
        }
    }
}
