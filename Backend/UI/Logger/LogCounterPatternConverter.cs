using log4net.Core;
using log4net.Layout.Pattern;
using System.Globalization;

namespace Academikus.AgenteInteligenteMentoresWebApi.WebApi.Logger
{
    /// <summary>
    /// Clase para manejar el contador de logs
    /// </summary>
    public sealed class LogCounterPatternConverter : PatternLayoutConverter
    {
        /// <summary>
        /// Método para convertir el log
        /// </summary>
        #region private static members
        private static int LogCounter;
        #endregion

        /// <summary>
        /// Método para convertir el log
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="loggingEvent"></param>
        #region protected override functions
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent)
        {
            writer.Write(LogCounter.ToString(CultureInfo.InvariantCulture));
            LogCounter++;
        }
        #endregion
    }
}
