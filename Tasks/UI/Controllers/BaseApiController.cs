using Microsoft.AspNetCore.Mvc;
using WebApiTemplate.AuthorizeDLL;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Common;
using Microsoft.Extensions.Options;

namespace Academikus.AgenteInteligenteMentoresTareas.WebApi.Controllers
{
    /// <summary>
    /// Controllador Base
    /// </summary>
    public abstract class BaseController<T> : Controller where T : BaseController<T>
    {
        private ILogger<T> _logger;
        protected ILogger<T> Logger => _logger ?? (_logger = HttpContext.RequestServices.GetService<ILogger<T>>());

        protected readonly WebApiBO _webApiBO;
        protected readonly AppSetting _appSetting;

        public BaseController(WebApiBO webApiBO, IOptions<AppSetting> appSetting)
        {
            _webApiBO = webApiBO;
            _appSetting = appSetting.Value;
            SetConfiguration();
        }

        private bool LogApiEnabled;

        private void SetConfiguration()
        {
            LogApiEnabled = _appSetting.LogApiEnabled;
        }

        /// <summary>
        /// Retorna el feature name.
        /// </summary>
        /// <returns></returns>
        protected string GetFeatureName()
        {
            string controller = ControllerContext.RouteData.Values["controller"].ToString();
            string action = ControllerContext.RouteData.Values["action"].ToString();

            var featureName = string.Format("{0}.{1}", controller, action);
            return featureName;
        }

        /// <summary>
        /// Registra la llamada al Api.
        /// </summary>
        /// <param name="webApiClientCode"></param>
        /// <param name="operationName"></param>
        /// <param name="parameter"></param>
        internal void LogApi(string webApiClientCode, string operationName, object parameter)
        {
            try
            {
                if (LogApiEnabled)
                {
                    string hostAddress = HttpContext.Connection.LocalIpAddress.ToString();
                    _webApiBO.LogApi(webApiClientCode, hostAddress, operationName, parameter);
                }
            }
            catch (Exception ex)
            {
                // Si ocurre un error en la actualización no lanzamos la excepción
                // Para evitar que no se ejecute el Api por esta razón.
                // Solo Registramos el error.
                _logger.LogError(ex, "No se pudo registra el log de uso del WebApi");
            }
        }
    }
}