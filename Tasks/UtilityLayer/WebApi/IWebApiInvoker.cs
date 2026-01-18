namespace Academikus.AgenteInteligenteMentoresTareas.Utility.WebApi
{
    /// <summary>
    /// Interfaz para clase estándar de consumos de APIS
    /// </summary>
    public interface IWebApiInvoker
    {
        Task SetBaseUrl(string baseAddress);
        Task<string> Post(string requestUri, object parameter, string securityToken);
        Task<string> Post(string requestUri, string jsonData, string securityToken);
        Task<string> Get(string requestUri, string securityToken);
        Task<string> Get(string requestUri, string user, string pass);
        Task<string> PostToken(string url, string grant_type, string userName, string password);
        Task<string> InvokeGet(string requestUri);
    }
}
