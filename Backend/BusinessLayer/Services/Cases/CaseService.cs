using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.ApiClient;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.TokenProvider;
using Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Semester;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.InPut;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using Academikus.AgenteInteligenteMentoresWebApi.Entity.OutPut;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using static Academikus.AgenteInteligenteMentoresWebApi.Utility.WebApi.WebApiInvoker;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Cases;

/// <summary>
/// Servicio encargado de gestionar la consulta y creación de casos en Salesforce.
/// Incluye lógica para obtener casos existentes y registrar nuevos casos.
/// </summary>
public class CaseService : ICaseService
{
    private readonly ISemesterService _semesterService;
    private readonly ISalesforceApiClient _salesforceApiClient;
    private readonly ISalesforceTokenProvider _salesforceTokenProvider;
    private readonly IConfiguration _configuration;

    public CaseService(
        ISemesterService semesterService,
        ISalesforceApiClient salesforceApiClient,
        ISalesforceTokenProvider salesforceTokenProvider,
        IConfiguration configuration)
    {
        _semesterService = semesterService;
        _salesforceApiClient = salesforceApiClient;
        _salesforceTokenProvider = salesforceTokenProvider;
        _configuration = configuration;
    }

    /// <summary>
    /// Obtiene el caso más reciente registrado en Salesforce para un usuario identificado por BannerId.
    /// </summary>
    /// <param name="bannerId">El BannerId del estudiante o mentor asociado a los casos.</param>
    /// <returns>
    /// Un objeto <see cref="SalesforceCase"/> correspondiente al caso más reciente,
    /// o <c>null</c> si no existen casos asociados.
    /// </returns>
    public async Task<SalesforceCase?> GetLastCaseByUserAsync(string bannerId)
    {
        // Obtiene todos los casos asociados al usuario desde Salesforce
        var allCases = await GetCasesByUserAsync(bannerId);

        // Ordena los casos por fecha de creación descendente (más reciente primero)
        // Si la fecha de creación es nula, se reemplaza por DateTimeOffset.MinValue para evitar errores
        var last = allCases?
            .OrderByDescending(c => c.CreatedDate ?? DateTimeOffset.MinValue)
            .FirstOrDefault();

        // Devuelve el caso más reciente o null si no existen
        return last;
    }

    /// <summary>
    /// Obtiene todos los casos asociados a un usuario en Salesforce mediante una consulta SOQL.
    /// </summary>
    /// <param name="bannerId">El BannerId del usuario cuyos casos se desean obtener.</param>
    /// <returns>
    /// Una lista de <see cref="SalesforceCase"/> correspondiente a los casos encontrados.
    /// Puede retornar una lista vacía si no existen registros.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando la configuración de Salesforce es inválida o incompleta.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Se produce cuando Salesforce devuelve un error HTTP o la respuesta no es válida.
    /// </exception>
    public async Task<IReadOnlyList<SalesforceCase>> GetCasesByUserAsync(string bannerId)
    {
        try
        {
            // Define configuraciones base de Salesforce
            var baseUrl = _configuration["Salesforce:BaseUrl"]
                ?? throw new InvalidOperationException("Salesforce:BaseUrl no configurado.");

            var apiBase = _configuration["Salesforce:RequestUri:ServiceVersion"]
                ?? throw new InvalidOperationException("Salesforce:RequestUri:ServiceVersion no configurado.");
            apiBase = apiBase.TrimEnd('/');

            // Carga la plantilla de SOQL desde el archivo de configuración y reemplaza el bannerId
            var soqlTemplate = _configuration["Salesforce:Querys:GetCases"]
                ?? throw new InvalidOperationException("Salesforce:Querys:GetCases no configurado en appsettings.");
            var soql = soqlTemplate.Replace("{bannerId}", bannerId, StringComparison.OrdinalIgnoreCase);

            // Codifica la consulta para incluirla en la URL
            var encoded = Uri.EscapeDataString(soql);

            // Construye endpoint completo
            var queryEndpoint = $"{apiBase}/query?q={encoded}";

            // Obtiene token de acceso válido
            var accessToken = await _salesforceTokenProvider.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("No se obtuvo access token de Salesforce.");

            // Instancia servicio REST y ejecuta GET hacia Salesforce
            var rawJson = await _salesforceApiClient.GetAsync(queryEndpoint, accessToken);

            // Deserializa la respuesta
            var result = JsonConvert.DeserializeObject<SalesforceQuery<SalesforceCase>>(rawJson ?? string.Empty);

            // Valida que el objeto no sea nulo o inválido
            if (result is null)
                throw new HttpRequestException("Respuesta inválida al consultar casos de Salesforce.");

            // Devuelve la lista de registros (ya ordenados por SOQL)
            return result.Records;
        }
        catch (HttpRequestException ex)
        {
            // Error de comunicación con Salesforce o respuesta inválida
            Console.WriteLine($"[Case][HTTP ERROR] Error al consultar casos de Salesforce: {ex.Message}");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            // Configuración incompleta o inválida
            Console.WriteLine($"[Case][CONFIG ERROR] Configuración de Salesforce incompleta: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            // Error inesperado
            Console.WriteLine($"[Case][UNEXPECTED ERROR] Error al obtener casos de Salesforce: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Crea un nuevo caso en Salesforce utilizando información del estudiante, mentor, 
    /// y los parámetros definidos en la configuración.
    /// </summary>
    /// <param name="bannerStudent">BannerId del estudiante asociado al caso.</param>
    /// <param name="bannerMentor">BannerId del mentor responsable del caso.</param>
    /// <param name="ownerEmail">
    /// Correo electrónico del usuario al que se asignará el caso.
    /// Si es vacío, el caso se asigna a una cola o grupo definido en la configuración.
    /// </param>
    /// <param name="subject">Asunto del caso, generado o concatenado según reglas de negocio.</param>
    /// <param name="theme">Tema del caso, utilizado para determinar metainformación.</param>
    /// <param name="summary">Descripción del caso o resumen del motivo de atención.</param>
    /// <param name="nextDate">Fecha programada para el siguiente seguimiento.</param>
    /// <returns>
    /// Un objeto <see cref="CreateCaseResponse"/> indicando éxito o fallas en la creación del caso.
    /// </returns>
    public async Task<CreateCaseResponse> CreateCaseAsync(
        string bannerStudent,
        string bannerMentor,
        string ownerEmail,
        string subject,
        string theme,
        string summary,
        DateOnly nextDate)
    {
        try
        {
            // Lee parámetros desde el archivo de configuración
            string status = _configuration["Salesforce:Case:Status"] ?? "No Atendido";
            string priority = _configuration["Salesforce:CaseDefaults:Priority"] ?? "Baja";
            string origin = _configuration["Salesforce:Case:Origin"] ?? "Mentor IA";
            string trackingStatus = _configuration["Salesforce:Case:TrackingStatus"];
            string wayContact = _configuration["Salesforce:Case:WayContact"];
            string typeContact = _configuration["Salesforce:Case:TypeContact"];
            string personContacted = _configuration["Salesforce:Case:PersonContacted"];
            string contactStatus = _configuration["Salesforce:Case:ContactStatus"];            
            string dateFormat = _configuration["Salesforce:Case:DateFormat"] ?? "yyyy-MM-dd";
            //string university = _configuration["Salesforce:Case:UniversidadInteres"] ?? "Ninguna";

            // Variables de uso interno
            string attentionResult1 = "";
            string attentionResult2 = "";
            string description = summary;

            // Obtiene el semestre actual
            var currentSemester = await _semesterService.GetCurrentSemesterCodeAsync() ?? "N/A";

            // Normaliza el tema (minusculas y sin tildes)
            var normalizedTheme = NormalizeEs(theme);

            // Determina resultados de atención según el tema
            if (normalizedTheme == "justificacion de falta" || normalizedTheme == "justificacion de faltas")
            {
                attentionResult1 = _configuration["Salesforce:Case:AbsenceJustification:AttentionResult1"];
                attentionResult2 = _configuration["Salesforce:Case:AbsenceJustification:AttentionResult2"];
            }
            else if (normalizedTheme == "consultas generales" || normalizedTheme == "consulta general")
            {
                attentionResult1 = _configuration["Salesforce:Case:GeneralConsultation:AttentionResult1"];
                attentionResult2 = _configuration["Salesforce:Case:GeneralConsultation:AttentionResult2"];
            }

            // Construye el asunto concatenando el anterior con el semestre actual y el tema
            subject = CaseSubjectBuilder.BuildNextSubject(summary, currentSemester, theme);

            //// ContactId (estudiante)
            //var contactId = await ResolveContactIdByBannerAsync(bannerStudent);
            //if (contactId == null)
            //{
            //    return new CreateCaseResponse
            //    {
            //        Success = false,
            //        Errors = new List<object> { $"No se encontró ContactId para Banner: {bannerStudent}" }
            //    };
            //}

            //// Persona responsable (mentor)
            //var responsibleId = await ResolveUserIdByBannerAsync(bannerMentor);
            //if (responsibleId == null)
            //{
            //    return new CreateCaseResponse
            //    {
            //        Success = false,
            //        Errors = new List<object> { $"No se encontró Persona_responsable__c para Banner: {bannerMentor}" }
            //    };
            //}

            //// OwnerId: puede ser User o Queue
            //string? ownerId;
            //if (string.IsNullOrWhiteSpace(ownerEmail))
            //{
            //    var queueName = _configuration["Salesforce:Case:GroupName"] ?? "Mentoría cola";

            //    ownerId = await ResolveQueueIdByNameAsync(queueName);
            //    if (ownerId == null)
            //    {
            //        return new CreateCaseResponse
            //        {
            //            Success = false,
            //            Errors = new List<object> { $"No se encontró cola de Salesforce con nombre: {queueName}" }
            //        };
            //    }
            //}
            //else
            //{
            //    ownerId = await ResolveUserIdByEmailAsync(ownerEmail);
            //    if (ownerId == null)
            //    {
            //        return new CreateCaseResponse
            //        {
            //            Success = false,
            //            Errors = new List<object> { $"No se encontró usuario Salesforce con email: {ownerEmail}" }
            //        };
            //    }
            //}

            //await PrintAllSalesforceContactsAsync();


            //string? recordTypeName = _configuration["Salesforce:Case:RecordType"]
            //                     ?? "Mentoria";

            //var recordTypeId = await ResolveRecordTypeIdAsync("Case", recordTypeName);

            var accessToken = await _salesforceTokenProvider.GetTokenAsync();

            //var describe = await _salesforceApiClient.GetAsync("/services/data/v61.0/sobjects/Case/describe", accessToken);

            //Console.WriteLine(describe);

            //await PrintSalesforceCaseFieldsAsync();

            //var contactStatusValues =
            //    await _salesforceApiClient.GetCasePicklistValuesAsync("Estado_contacto__c", accessToken);

            //Console.WriteLine("Valores válidos de Estado_contacto__c:");
            //foreach (var v in contactStatusValues)
            //    Console.WriteLine($"- {v}");

            //var seguimientoValues =
            //    await _salesforceApiClient.GetCasePicklistValuesAsync("Estado_seguimiento__c", accessToken);

            //Console.WriteLine("Valores válidos de Estado_seguimiento__c:");
            //foreach (var v in seguimientoValues)
            //    Console.WriteLine($"- {v}");

            OwnerRequest owner;

            //owner = new OwnerRequest
            //{
            //    Attributes = new AttributeRequest("User"),
            //    Email = ownerEmail
            //};

            owner = new OwnerRequest
            {
                Attributes = new AttributeRequest("User"),
                Email = "gabriela.lopez.sosa@udla.edu.ec"
            };

            var recordTypeName =
                _configuration["Salesforce:Case:RecordType"] ?? "Mentoria";

            // Crea modelo base para el caso
            var model = new Entity.Models.CreateCaseRequest
            {
                Status = status,
                Origin = origin,
                TrackingStatus = trackingStatus,
                ContactStatus = contactStatus,
                WayContact = wayContact,
                TypeContact = typeContact,

                PersonContacted = personContacted,
                Contact = new BannerCodeRequest(bannerStudent),
                ResponsiblePerson = new BannerCodeRequest("A00020095"),
                RecordType = new RecordTypeRequest(recordTypeName),

                NextDate = nextDate.ToString(dateFormat, CultureInfo.InvariantCulture),

                Subject = subject,
                Description = description,
                AttentionResult1 = attentionResult1,
                AttentionResult2 = attentionResult2,
                //UniversidadInteres = university,

                Priority = priority,
                
                // Owner = owner
            };
          
            // Serializa el modelo a JSON
            var jsonData = JsonConvert.SerializeObject(model);

            // Valida configuración de Salesforce
            var baseUrl = _configuration["Salesforce:BaseUrl"]
                ?? throw new InvalidOperationException("Salesforce:BaseUrl no configurado.");
            var apiBase = _configuration["Salesforce:RequestUri:ServiceVersion"]
                ?? throw new InvalidOperationException("Salesforce:RequestUri:ServiceVersion no configurado.");
            apiBase = apiBase.TrimEnd('/');

            // Obtiene el token de acceso
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new InvalidOperationException("No se obtuvo access token de Salesforce.");

            // Construye el endpoint final para inserción
            var insertTpl = _configuration["Salesforce:RequestUri:Insert"] ?? "/sobjects";
            var objectName = _configuration["Salesforce:Objects:Case"] ?? "Case";
            var requestUri = $"{baseUrl}{apiBase}/{insertTpl}/{objectName}";

            Console.WriteLine("========== SALESFORCE CASE PAYLOAD ==========");
            Console.WriteLine(jsonData);
            Console.WriteLine("============================================");

            // Ejecuta la solicitud POST
            var resultJson = await _salesforceApiClient.PostAsync(requestUri, accessToken, jsonData);

            // Deserializa la respuesta
            var response = JsonConvert.DeserializeObject<CreateCaseResponse>(resultJson)
                           ?? new CreateCaseResponse { Success = false, Errors = new List<object> { "Respuesta vacía" } };

            // Devuelve la respuesta del servicio
            return response;
        }
        catch (HttpRequestException ex)
        {
            // Error de comunicación con Salesforce (red, timeout, etc.)
            Console.WriteLine($"[Case][HTTP ERROR] Error al crear caso en Salesforce: {ex.Message}");
            return new CreateCaseResponse
            {
                Success = false,
                Errors = new List<object> { $"Error de conexión con Salesforce: {ex.Message}" }
            };
        }
        catch (InvalidOperationException ex)
        {
            // Error de configuración o token inválido
            Console.WriteLine($"[Case][CONFIG ERROR] Configuración de Salesforce incompleta: {ex.Message}");
            return new CreateCaseResponse
            {
                Success = false,
                Errors = new List<object> { $"Configuración inválida: {ex.Message}" }
            };
        }
        catch (Exception ex)
        {
            // Error inesperado (cualquier otra excepción)
            Console.WriteLine($"[Case][UNEXPECTED ERROR] Error inesperado en CreateCaseAsync: {ex}");
            return new CreateCaseResponse
            {
                Success = false,
                Errors = new List<object> { $"Error inesperado: {ex.Message}" }
            };
        }
    }

    private async Task<string?> ResolveContactIdByBannerAsync(string bannerCode)
    {
        var token = await _salesforceTokenProvider.GetTokenAsync();

        var soql = $"SELECT Id FROM Contact WHERE Codigo_banner__c = '{bannerCode}' LIMIT 1";

        return await _salesforceApiClient.QuerySingleValueAsync(soql, "Id", token);
    }

    private async Task<string?> ResolveUserIdByBannerAsync(string bannerCode)
    {
        if (string.IsNullOrWhiteSpace(bannerCode))
            return null;

        var token = await _salesforceTokenProvider.GetTokenAsync();
        var soql = $"SELECT Id FROM User WHERE Codigo_usuario_banner__c = '{bannerCode}' LIMIT 1";

        return await _salesforceApiClient.QuerySingleValueAsync(soql, "Id", token);
    }

    private async Task<string?> ResolveUserIdByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var token = await _salesforceTokenProvider.GetTokenAsync();
        var soql = $"SELECT Id FROM Contact WHERE Email = '{email}' LIMIT 1";

        return await _salesforceApiClient.QuerySingleValueAsync(soql, "Id", token);
    }

    private async Task<string?> ResolveQueueIdByNameAsync(string queueName)
    {
        if (string.IsNullOrWhiteSpace(queueName))
            return null;

        var token = await _salesforceTokenProvider.GetTokenAsync();
        var soql =
            $"SELECT Id FROM Group WHERE Name = '{queueName}' AND Type = 'Queue' LIMIT 1";

        return await _salesforceApiClient.QuerySingleValueAsync(soql, "Id", token);
    }

    private async Task<string?> ResolveRecordTypeIdAsync(string objectName, string recordTypeName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || string.IsNullOrWhiteSpace(recordTypeName))
            return null;

        var token = await _salesforceTokenProvider.GetTokenAsync();

        var soql =
            $"SELECT Id FROM RecordType " +
            $"WHERE SobjectType = '{objectName}' " +
            $"AND DeveloperName = '{recordTypeName}' " +
            $"LIMIT 1";

        return await _salesforceApiClient.QuerySingleValueAsync(soql, "Id", token);
    }

    public async Task PrintSalesforceCaseFieldsAsync()
    {
        var accessToken = await _salesforceTokenProvider.GetTokenAsync();
        var describe = await _salesforceApiClient.GetAsync("/services/data/v61.0/sobjects/Case/describe", accessToken);

        dynamic json = JsonConvert.DeserializeObject(describe);

        Console.WriteLine("====== CAMPOS DEL OBJETO CASE ======");

        foreach (var field in json.fields)
        {
            string name = field.name;
            string label = field.label;
            string type = field.type;

            bool nillable = field.nillable;
            bool defaulted = field.defaultedOnCreate;
            bool createable = field.createable;

            // Campo obligatorio si:
            // - No es nillable
            // - No tiene valor por defecto
            // - Es creable (disponible para insert)
            bool required = (!nillable && !defaulted && createable);

            Console.WriteLine(
                $"{(required ? "[OBLIGATORIO] " : "[opcional]    ")} {name} ({label}) - tipo: {type}"
            );
        }

        Console.WriteLine("====== FIN DE LISTA ======");
    }

    public async Task PrintAllSalesforceContactsAsync()
    {
        var accessToken = await _salesforceTokenProvider.GetTokenAsync();

        // Consulta básica
        string soql = "SELECT Id, Name, Email FROM Contact ORDER BY Name";

        string queryUri = $"/services/data/v61.0/query?q={Uri.EscapeDataString(soql)}";

        Console.WriteLine("===== LISTA DE CONTACTOS EN SALESFORCE =====");

        while (true)
        {
            // Ejecuta consulta
            var raw = await _salesforceApiClient.GetAsync(queryUri, accessToken);

            dynamic json = JsonConvert.DeserializeObject(raw);

            // Imprime contactos
            foreach (var record in json.records)
            {
                string id = record.Id;
                string name = record.Name;
                string email = record.Email != null ? (string)record.Email : "(sin email)";

                Console.WriteLine($"Id: {id}  |  Email: {email}  |  Nombre: {name}");
            }

            // Si no hay más páginas → terminamos
            if (json.nextRecordsUrl == null)
                break;

            // Siguiente página
            queryUri = json.nextRecordsUrl;
        }

        Console.WriteLine("===== FIN DE LISTA =====");
    }



    // Normaliza: trim, minúsculas y sin acentos/diacríticos
    private static string NormalizeEs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var lower = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lower.Length);

        foreach (var ch in lower)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

}
