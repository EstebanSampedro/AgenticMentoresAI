using Academikus.AgenteInteligenteMentoresWebApi.Utility.Mapper;
using Oracle.ManagedDataAccess.Client;
using System.Configuration;
using System.Data;

namespace Udla.ElectronicApprovalWinService.Data.AdoNet
{
    /// <summary>
    /// Contiene los comandos para conectarse con una base de datos MS SQL Server
    /// </summary>
    /// 
    public class OracleConexionDataBase
    {
        /// <summary>
        /// Ejecuta una consulta SQL (Select) sobre la base de datos Oracle
        /// </summary>
        /// <param name="query">La consulta a ejecutar (Select)</param>
        /// <returns>El DataSet con los datos recibidos</returns>
        public static DataSet Query(string query)
        {
            OracleConnection connection = ConstruirConexionDB();

            try
            {
                connection.Open();
                OracleDataAdapter dataAdapter = new OracleDataAdapter();
                DataSet dataSet = new DataSet();
                dataAdapter.SelectCommand = new OracleCommand(query, connection);
                dataAdapter.Fill(dataSet);
                return dataSet;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Query vistas banner
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static DataSet QueryBanner(string query)
        {
            OracleConnection connection = ConstruirConexionDB();
            DataSet DsReturnQuery = new DataSet();
            try
            {
                connection.Open();

                OracleCommand SqlCmd = new OracleCommand(query, connection);
                SqlCmd.CommandType = System.Data.CommandType.Text;
                OracleDataAdapter SqlDataAdapter = new OracleDataAdapter();
                SqlDataAdapter.SelectCommand = SqlCmd;
                SqlDataAdapter.Fill(DsReturnQuery);

                return DsReturnQuery;

            }

            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Ejecuta una sentencia (Insert, Update, Delete) SQL sobre la base de datos Oracle.
        /// </summary>
        /// <param name="strSentence">La sentencia a ejecutar</param>
        /// <returns>El resultado de que si se ejecuto o no la sentencia</returns>
        public static bool Execute(string strSentence)
        {
            using (OracleConnection connection = ConstruirConexionDB())
            {
                connection.Open();

                using (OracleTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        OracleCommand command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = strSentence;
                        command.ExecuteNonQuery();

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Commit();
                        throw ex;
                    }
                    finally
                    {
                        connection.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Ejecuta un procedimiento almacenado que no sea una consulta
        /// </summary>
        /// <param name="nombreStoredProcedure">El procedimiento almacenado a ejecutarse</param>
        /// <param name="parametros">Los parametros que recibe el procedimiento almacenado</param>
        /// <returns>Si hay algún error, devuelve el texto que nos da el SP. Si se ejecutó correctamente, devuelve un string vacío</returns>
        public static string ExecuteStoredProcedure(string nombreStoredProcedure, Dictionary<string, object> parametros2)
        {
            OracleConnection connection = ConstruirConexionDB();
            try
            {
                OracleCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = nombreStoredProcedure;

                foreach (var item in parametros2)
                {
                    command.Parameters.Add(item.Key, item.Value);
                }

                if (command.Parameters["P_ERROR"] != null)
                {
                    command.Parameters["P_ERROR"].Size = 2000;
                    //command.Parameters["P_ERROR"].OracleDbType = OracleDbType.Varchar2;
                    command.Parameters["P_ERROR"].Direction = ParameterDirection.Output;
                }

                connection.Open();

                command.ExecuteNonQuery();

                string error = command.Parameters["P_ERROR"].Value.ToString();

                return error;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Ejecuta un procedimiento almacenado que no sea una consulta
        /// </summary>
        /// <param name="nombreStoredProcedure">El procedimiento almacenado a ejecutarse</param>
        /// <param name="parametros">Los parametros que recibe el procedimiento almacenado</param>
        /// <returns>Si hay algún error, devuelve el texto que nos da el SP. Si se ejecutó correctamente, devuelve un string vacío</returns>
        public static void ExecuteStoredProcedure(
            string nombreStoredProcedure,
            Dictionary<string, object> parametros2,
            out string valida,
            out string mensaje)
        {
            OracleConnection connection = ConstruirConexionDB();
            try
            {
                OracleCommand command = connection.CreateCommand();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = nombreStoredProcedure;

                foreach (var item in parametros2)
                {
                    command.Parameters.Add(item.Key, item.Value);
                }

                if (command.Parameters["P_VALIDA"] != null)
                {
                    command.Parameters["P_VALIDA"].Size = 2000;
                    //command.Parameters["P_VALIDA"].OracleDbType = OracleDbType.Varchar2;
                    command.Parameters["P_VALIDA"].Direction = ParameterDirection.Output;
                }

                if (command.Parameters["P_MENSAJE"] != null)
                {
                    command.Parameters["P_MENSAJE"].Size = 2000;
                    //command.Parameters["P_MENSAJE"].OracleDbType = OracleDbType.Varchar2;
                    command.Parameters["P_MENSAJE"].Direction = ParameterDirection.Output;
                }

                connection.Open();

                command.ExecuteNonQuery();

                valida = command.Parameters["P_VALIDA"].Value.ToString();
                mensaje = command.Parameters["P_MENSAJE"].Value.ToString();

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Ejecuta un procedimiento almacenado que no sea una consulta
        /// </summary>
        /// <returns></returns>
        private static OracleConnection ConstruirConexionDB()
        {
            OracleConnection connection = null;
            string connectionString = string.Empty;

            string oracleMinConnectionString = ConfigurationManager.ConnectionStrings["BannerConnection_ConnectionString"].ConnectionString;

            connection = new OracleConnection(oracleMinConnectionString/*connectionString*/);

            return connection;
        }

        /// <summary>
        /// Obtiene una lista de objetos de un DataTable
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="dataTableInformation"></param>
        /// <returns></returns>
        public static IList<TModel> ObtenerLista<TModel>(DataTable dataTableInformation)
        {

            var listaResultante = dataTableInformation.MapDataTableToList<TModel>();

            return listaResultante;
        }
    }
}
