using System.Data.SqlClient;
using System.Data;
using System.Reflection;
using Dapper;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.Command.Core
{
    /// <summary>
    /// Clase utilizada para ejecutar comandos en la base de datos
    /// </summary>
    public class CommandExecutor : ICommandExecutor
    {
        /// <summary>
        /// Ejecuta una consulta SQL (Select) sobre la base de datos MS SQL Server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connectionstring"></param>
        /// <param name="sqlQuery"></param>
        /// <returns></returns>
        public IEnumerable<T> GetSqlData<T>(string connectionstring, string sqlQuery) where T : new()
        {
            var properties = typeof(T).GetProperties();

            using (var conn = new SqlConnection(connectionstring))
            {
                using (var comm = new SqlCommand(sqlQuery, conn))
                {
                    conn.Open();
                    using (var reader = comm.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var element = new T();

                            foreach (var f in properties)
                            {
                                var o = reader[f.Name];
                                if (o.GetType() != typeof(DBNull)) f.SetValue(element, o, null);
                            }
                            yield return element;
                        }
                    }
                    conn.Close();
                }
            }
        }

        /// <summary>
        /// Ejecuta una consulta SP sobre la base de datos MS SQL Server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connectionstring"></param>
        /// <param name="spName"></param>
        /// <param name="parametros"></param>
        /// <returns></returns>
        public async Task<List<T>> GetAsyncListFromSP<T>(string connectionstring, string spName, object parametros = null)
        {
            using var connection = new SqlConnection(connectionstring);
            try
            {
                await connection.OpenAsync();
                var result = await connection.QueryAsync<T>(
                    sql: spName,
                    param: parametros,
                    commandType: CommandType.StoredProcedure
                );

                return result.AsList();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                connection.Close();
            }

        }

        /// <summary>
        /// Ejecuta una consulta SP sobre la base de datos MS SQL Server
        /// </summary>
        /// <param name="connectionstring"></param>
        /// <param name="spName"></param>
        /// <param name="parametros"></param>
        /// <returns></returns>
        public async Task ExecuteAsyncFromSP(string connectionstring, string spName, object parametros = null)
        {
            using var connection = new SqlConnection(connectionstring);
            try
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    sql: spName,
                    param: parametros,
                    commandType: CommandType.StoredProcedure);
                //return listaRes;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Ejecuta una consulta SQL (Insert) sobre la base de datos MS SQL Server
        /// </summary>
        /// <param name="connectionstring"></param>
        /// <param name="sqlQuery"></param>
        /// <returns></returns>
        public async Task InsertData(string connectionstring, string sqlQuery)
        {
            SqlConnection sqlConnection = new SqlConnection(connectionstring);

            SqlCommand cmd = new SqlCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sqlQuery;
            cmd.Connection = sqlConnection;

             await sqlConnection.OpenAsync();
            cmd.ExecuteNonQuery();
            sqlConnection.Close();
        }

        /// <summary>
        /// Devuelve una lista de objetos de tipo T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dr"></param>
        /// <returns></returns>
        private List<T> DataReaderMapToList<T>(IDataReader dr)
        {
            List<T> list = new List<T>();
            T obj = default;
            while (dr.Read())
            {
                obj = Activator.CreateInstance<T>();
                foreach (PropertyInfo prop in obj.GetType().GetProperties())
                {
                    if (!Equals(dr[prop.Name], DBNull.Value))
                    {
                        prop.SetValue(obj, dr[prop.Name], null);
                    }
                }
                list.Add(obj);
            }
            return list;
        }
    }
}
