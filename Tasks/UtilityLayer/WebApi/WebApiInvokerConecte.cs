using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresTareas.Utility.WebApi
{
    /// <summary>
    /// Clase para obtener información de conecte API
    /// </summary>
    public class WebApiInvokerConecte : WebApiInvoker, IWebApiInvokerConecte
    {
        protected readonly ILogger<WebApiInvoker> _logger;

        /// <summary>
        /// Constructor de clase
        /// </summary>
        /// <param name="logger"></param>
        public WebApiInvokerConecte(ILogger<WebApiInvoker> logger) : base(logger)
        {
        }
        
        /// <summary>
        ///  Petición post para obtener información de conecte
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public async Task<Dictionary<HttpResponseMessage, string>> InvokePostConecte(string requestUri, object parameter)
        {
            try
            {
                Dictionary<HttpResponseMessage, string> response = new Dictionary<HttpResponseMessage, string>();
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseAddress);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var jsonRequest = JsonConvert.SerializeObject(parameter);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    HttpResponseMessage httpResponse = client.PostAsync(requestUri, content).GetAwaiter().GetResult();

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        response.Add(httpResponse, httpResponse.Content.ReadAsStringAsync().Result);
                    }
                    else
                    {
                        response.Add(httpResponse, "Error Code");
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ha ocurrido un error al invocar el InvokePOST");
                throw;
            }
        }
    }
}
