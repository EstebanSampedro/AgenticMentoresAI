using log4net.Layout.Pattern;

namespace Academikus.AgenteInteligenteMentoresTareas.WebApi.Logger
{
    /// <summary>
    /// Clase para manejar el nombre del producto
    /// </summary>
    public class ProductNamePatternConverter : PatternLayoutConverter
    {
        /// <summary>
        /// Método para obtener el nombre del producto
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="loggingEvent"></param>
        protected override void Convert(TextWriter writer, log4net.Core.LoggingEvent loggingEvent)
        {
            string nameApp = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            writer.Write(nameApp);
        }
    }
}
