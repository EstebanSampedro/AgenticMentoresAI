using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.ApiClient;

/// <summary>
/// Cliente HTTP especializado para comunicarse con los servicios REST de Salesforce.
/// 
/// Esta clase centraliza las operaciones GET, POST y de solicitud de token OAuth,
/// utilizando un <see cref="HttpClient"/> configurado previamente con la URL base.
/// 
/// Es un componente de bajo nivel, utilizado por servicios superiores para
/// interactuar con Salesforce sin duplicar la lógica de encabezados, autenticación
/// o manejo de errores HTTP.
/// </summary>
public class SalesforceApiClient : ISalesforceApiClient
{
    private readonly HttpClient _client;

    /// <summary>
    /// Inicializa el cliente usando un <see cref="HttpClient"/> inyectado,
    /// que debe estar configurado con la URL base de Salesforce.
    /// </summary>
    public SalesforceApiClient(HttpClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Ejecuta una solicitud GET contra un endpoint de Salesforce, adjuntando el token OAuth.
    /// 
    /// Este método prepara los encabezados necesarios, envía la solicitud
    /// y devuelve el cuerpo de la respuesta como texto crudo.
    /// Si la respuesta no es exitosa, lanza una excepción con detalles del error.
    /// </summary>
    /// <param name="requestUri">Ruta relativa del endpoint Salesforce (por ejemplo: "/services/data/v61.0/query?q=...").</param>
    /// <param name="accessToken">Token de acceso válido obtenido mediante OAuth.</param>
    public async Task<string> GetAsync(string requestUri, string accessToken)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, requestUri);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var resp = await _client.SendAsync(req);

            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Salesforce GET falló. Status {(int)resp.StatusCode}: {raw}");

            return raw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesforceApiClient][GET ERROR] {ex}");
            throw;
        }
    }

    public async Task<string?> QuerySingleValueAsync(string soql, string field, string accessToken)
    {
        try
        {
            var encodedSoql = Uri.EscapeDataString(soql);
            var requestUri = $"/services/data/v61.0/query?q={encodedSoql}";

            var req = new HttpRequestMessage(HttpMethod.Get, requestUri);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var resp = await _client.SendAsync(req);
            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Salesforce SOQL failed. Status {(int)resp.StatusCode}: {raw}");

            dynamic json = JsonConvert.DeserializeObject(raw)!;

            if (json.records == null || json.records.Count == 0)
                return null;

            return (string?)json.records[0][field];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesforceApiClient][QUERY ERROR] {ex}");
            throw;
        }
    }


    /// <summary>
    /// Ejecuta una solicitud POST contra un endpoint Salesforce enviando un cuerpo JSON,
    /// utilizando un token OAuth para autenticación.
    /// 
    /// Este método es utilizado para operaciones de inserción o actualización que
    /// requieren un payload estructurado (por ejemplo: creación de casos).
    /// </summary>
    /// <param name="requestUri">Ruta relativa del endpoint a invocar.</param>
    /// <param name="accessToken">Token OAuth válido.</param>
    /// <param name="jsonBody">Contenido JSON serializado a enviar en la solicitud.</param>
    public async Task<string> PostAsync(string requestUri, string accessToken, string jsonBody)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, requestUri);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var resp = await _client.SendAsync(req);
            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Salesforce POST falló. Status {(int)resp.StatusCode}: {raw}");

            return raw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesforceApiClient][POST ERROR] {ex}");
            throw;
        }
    }    

    /// <summary>
    /// Ejecuta una solicitud POST al endpoint de autenticación OAuth de Salesforce,
    /// enviando los parámetros necesarios para obtener un token de acceso.
    /// 
    /// Este método utiliza <c>x-www-form-urlencoded</c> como lo exige Salesforce
    /// para el flujo de usuario + password + token.
    /// </summary>
    /// <param name="tokenRelativeUrl">Ruta relativa del endpoint OAuth (generalmente "/services/oauth2/token").</param>
    /// <param name="form">Colección clave-valor con parámetros como grant_type, client_id, client_secret, username y password.</param>
    public async Task<string> RequestTokenAsync(
        string tokenRelativeUrl,
        IEnumerable<KeyValuePair<string, string>> form)
    {
        try
        {
            var content = new FormUrlEncodedContent(form);
            var resp = await _client.PostAsync(tokenRelativeUrl, content);
            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Salesforce token request falló. Status {(int)resp.StatusCode}: {raw}");

            return raw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SalesforceApiClient][TOKEN ERROR] {ex}");
            throw;
        }
    }

    public async Task<List<string>> GetCasePicklistValuesAsync(string fieldName, string accessToken)
    {
        var describeUri = "/services/data/v61.0/sobjects/Case/describe";
        var raw = await GetAsync(describeUri, accessToken);

        dynamic json = JsonConvert.DeserializeObject(raw);

        foreach (var field in json.fields)
        {
            if (field.name == fieldName)
            {
                var list = new List<string>();
                foreach (var item in field.picklistValues)
                {
                    if ((bool)item.active)
                    {
                        list.Add((string)item.value);
                    }
                }
                return list;
            }
        }

        return new List<string>();
    }

    public async Task<dynamic> DescribeObjectAsync(string objectName, string accessToken)
    {
        var uri = $"/services/data/v61.0/sobjects/{objectName}/describe";
        var raw = await GetAsync(uri, accessToken);
        return JsonConvert.DeserializeObject(raw);
    }

    public async Task<List<string>> GetRequiredFieldsAsync(
    string objectName,
    string recordTypeId,
    string accessToken)
    {
        var describe = await DescribeObjectAsync(objectName, accessToken);

        List<string> required = new List<string>();

        foreach (var field in describe.fields)
        {
            bool nillable = field.nillable;
            bool defaulted = field.defaultedOnCreate;
            bool createable = field.createable;
            string name = field.name;

            // Si el campo no es nillable y no tiene valor por defecto
            if (!nillable && !defaulted && createable)
            {
                required.Add(name);
            }

            // Validar obligatoriedad por Record Type
            if (field.recordTypeInfos != null)
            {
                foreach (var rt in field.recordTypeInfos)
                {
                    if (rt.recordTypeId == recordTypeId && rt.required == true)
                    {
                        required.Add(name);
                    }
                }
            }
        }

        return required.Distinct().ToList();
    }

    public List<string> ValidateModelAgainstRequiredFields(
    object model,
    List<string> requiredFields,
    Dictionary<string, string>? fieldMappings = null)
    {
        List<string> errors = new();

        // Mappings entre tu model y Salesforce (opcional)
        // Ejemplo: "ContactId" -> "ContactId", "ResponsiblePersonId" -> "Persona_responsable__c"
        fieldMappings ??= new Dictionary<string, string>();

        var properties = model.GetType().GetProperties();

        foreach (var sfField in requiredFields)
        {
            // Buscar si existe mapping explícito
            var prop = properties.FirstOrDefault(p =>
                fieldMappings.ContainsKey(p.Name) && fieldMappings[p.Name] == sfField
                || p.Name.Equals(sfField, StringComparison.OrdinalIgnoreCase));

            if (prop == null)
            {
                errors.Add($"Campo obligatorio '{sfField}' no está presente en el modelo.");
                continue;
            }

            var value = prop.GetValue(model);

            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                errors.Add($"El campo obligatorio '{sfField}' no tiene valor.");
            }
        }

        return errors;
    }

}
