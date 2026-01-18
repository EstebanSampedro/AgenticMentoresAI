namespace Academikus.AgenteInteligenteMentoresTareas.Entity.Common
{
    /// <summary>
    /// Clase para manejar las cadenas de conexión
    /// </summary>
    public class ConnectionString
    {
        public string D2LIntegrationDBEntities { get; set; }
        public string WebApiDBEntities { get; set; }
        public string ScholarshipRequestConnection { get; set; }
        public string LoggerDBEntities { get; set; }
    }
}
