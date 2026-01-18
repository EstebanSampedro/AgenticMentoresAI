namespace Academikus.AgenteInteligenteMentoresWebApi.Utility.WebApi
{
    /// <summary>
    /// Custom WebApiInvoker
    /// </summary>
    public interface IWebApiInvokerConecte: IWebApiInvoker
    {
        Task<Dictionary<HttpResponseMessage, string>> InvokePostConecte(string requestUri, object parameter);
    }
}
