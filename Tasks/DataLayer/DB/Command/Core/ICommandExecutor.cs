namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.Command.Core
{
    /// <summary>
    /// Interfaz utilizada para ejecutar comandos en la base de datos
    /// </summary>
    public interface ICommandExecutor
    {
        IEnumerable<T> GetSqlData<T>(string connectionstring, string sql) where T : new();
        Task<List<T>> GetAsyncListFromSP<T>(string connectionstring, string spName, object parametros = null);
        Task ExecuteAsyncFromSP(string connectionstring, string spName, object parametros = null);
        Task InsertData(string connectionstring, string sqlQuery);
    }
}
