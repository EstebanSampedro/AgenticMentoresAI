using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Utility.WebApi
{
    /// <summary>
    /// Clase estándar de consumos de APIS
    /// </summary>
    public class WebApiInvoker : IWebApiInvoker
    {
        protected string BaseAddress { get; set; }
        protected readonly ILogger<WebApiInvoker> _logger;
        string GetActualAsyncMethodName([CallerMemberName] string name = null) => name;

        /// <summary>
        /// Constructor de clase
        /// </summary>
        /// <param name="logger"></param>
        public WebApiInvoker(ILogger<WebApiInvoker> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Setear dirección base
        /// </summary>
        /// <param name="baseAddress"></param>
        /// <returns></returns>
        public async Task SetBaseUrl(string baseAddress)
        {
            BaseAddress = baseAddress;
        }

        /// <summary>
        ///  Petición get mediante autenticación básica por paramétros
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public Task<string> Post(string requestUri, object parameter, string securityToken)
        {
            try
            {
                var jsonRequest = JsonConvert.SerializeObject(parameter);
                return this.Post(requestUri, jsonRequest, securityToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetActualAsyncMethodName()}: Ha ocurrido un error al invocar el POST");
                throw;
            }
        }

        /// <summary>
        ///  Petición post mediante autenticación básica
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<string> Post(string requestUri, string jsonData, string securityToken)
        {

            try
            {
                string response = string.Empty;
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseAddress);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", securityToken);

                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    HttpResponseMessage httpResponse = client.PostAsync(requestUri, content).GetAwaiter().GetResult();

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation($"La respuesta de la API ha sido incorrecta: {response}");
                        throw new ApplicationException(response);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetActualAsyncMethodName()}: Ha ocurrido un error al invocar el POST");
                throw;
            }
        }

        /// <summary>
        ///  Petición get mediante autenticación con token
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<string> Get(string requestUri, string securityToken)
        {
            try
            {
                string response = string.Empty;
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseAddress);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", securityToken);
                    client.Timeout = TimeSpan.FromMinutes(10);

                    HttpResponseMessage httpResponse = client.GetAsync(requestUri).GetAwaiter().GetResult();

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation($"No se obtuvo un código exitoso desde WebApi: Código {response}");
                        throw new ApplicationException(response);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetActualAsyncMethodName()}: Ha ocurrido un error al invocar el GET");
                throw;
            }
        }

        /// <summary>
        /// Petición get mediante autenticación básica
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<string> Get(string requestUri, string user, string pass)
        {
            try
            {
                string response = string.Empty;
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseAddress);

                    var authenticationString = $"{user}:{pass}";
                    var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedAuthenticationString);
                    client.Timeout = TimeSpan.FromMinutes(10); // TimeOut

                    HttpResponseMessage httpResponse = client.GetAsync(requestUri).GetAwaiter().GetResult();

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation($"No se obtuvo un código exitoso desde WebApi: Código {response}");
                        throw new ApplicationException(response);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetActualAsyncMethodName()}: Ha ocurrido un error al invocar el GET");
                throw;
            }
        }

        /// <summary>
        /// Clase de respuesta de token
        /// </summary>
        public class Token
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Grant_type { get; set; }
        }

        /// <summary>
        /// Método para obtener token
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private string SecurityToken(string requestUri, List<KeyValuePair<string, string>> parameter)
        {
            try
            {
                string response = string.Empty;

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseAddress);

                    using (var content = new FormUrlEncodedContent(parameter))
                    {
                        content.Headers.Clear();
                        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                        HttpResponseMessage httpResponse = client.PostAsync(requestUri, content).GetAwaiter().GetResult();

                        if (httpResponse.IsSuccessStatusCode)
                        {
                            response = httpResponse.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            response = httpResponse.Content.ReadAsStringAsync().Result;
                            _logger.LogInformation($"No se obtuvo un código exitoso al intentar obtener el token: Código {response}");
                            throw new ApplicationException(response);

                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetActualAsyncMethodName()}: Ha ocurrido un error al intentar obtener el token");
                throw;
            }
        }

        /// <summary>
        /// Función para obtener el token de cualquier API Post
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<string> PostToken(string url, string grant_type, string userName, string password)
        {
            try
            {
                var data = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("grant_type", grant_type),
                    new KeyValuePair<string, string>("username", userName),
                    new KeyValuePair<string, string>("password", password)
                };

                string res = SecurityToken(url, data);

                if (string.IsNullOrEmpty(res))
                {
                    return string.Empty;
                }

                string token = JsonConvert.DeserializeObject<dynamic>(res).access_token;
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetActualAsyncMethodName()}: Ha ocurrido un error al invocar el POST del token");
                throw;
            }
        }

        /// <summary>
        /// Método para peticiones Get
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public async Task<string> InvokeGet(string requestUri)
        {
            try
            {
                string response = string.Empty;
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseAddress);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    HttpResponseMessage httpResponse = client.GetAsync(requestUri).GetAwaiter().GetResult();

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        response = await httpResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation($"No se obtuvo un código exitoso al intentar obtener el get: Código {response}");
                        throw new ApplicationException(response);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{GetActualAsyncMethodName()}: Ha ocurrido un error al invocar el InvokeGET");
                throw;
            }
        }
    }
}
