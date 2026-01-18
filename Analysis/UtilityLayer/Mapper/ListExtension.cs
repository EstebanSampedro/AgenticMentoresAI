//using AutoMapper;
//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using Academikus.AnalysisMentoresVerdes.Utility.General;

//namespace Academikus.AnalysisMentoresVerdes.Utility.Mapper
//{
//    /// <summary>
//    /// Extensión del List para hacer un mapeo entre obejtos tipados.
//    /// </summary>
//    public static class ListExtension
//    {

//        /// <summary>
//        /// Función que mapea automáticamente un objeto List de un tipo especificado a un List de otro tipo.
//        /// Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
//        /// </summary>
//        /// <typeparam name="TSource">Tipo de datos fuente.</typeparam>
//        /// <typeparam name="TDestination">Tipo de datos destino a donde le vamos a pasar la información desde el fuente.</typeparam>
//        /// <param name="list">Extensión de List que deseamos mapear al ViewModel.</param>
//        /// <returns>Lista con el tipo de datos destino.</returns>
//        public static List<TDestination> ToMappedList<TSource, TDestination>(this List<TSource> list)
//        {
//            return ToMappedList<TSource, TDestination>(list, null, null);
//        }

//        /// <summary>
//        /// Función que mapea automáticamente un objeto List de un tipo especificado a un List de otro tipo.
//        /// Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
//        /// </summary>
//        /// <typeparam name="TSource">Tipo de datos fuente.</typeparam>
//        /// <typeparam name="TDestination">Tipo de datos destino a donde le vamos a pasar la información desde el fuente.</typeparam>
//        /// <param name="list">Extensión de List que deseamos mapear al ViewModel.</param>
//        /// <param name="secuencial">Nombre de la propiedad sobre la que se aplicará un secuencial.</param>
//        /// <returns>Lista con el tipo de datos destino.</returns>
//        public static List<TDestination> ToMappedList<TSource, TDestination>(this List<TSource> list, string secuencial)
//        {
//            return ToMappedList<TSource, TDestination>(list, null, secuencial);
//        }

//        /// <summary>
//        /// Función que mapea automáticamente un objeto List de un tipo especificado a un List de otro tipo.
//        /// Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
//        /// </summary>
//        /// <typeparam name="TSource">Tipo de datos fuente.</typeparam>
//        /// <typeparam name="TDestination">Tipo de datos destino a donde le vamos a pasar la información desde el fuente.</typeparam>
//        /// <param name="list">Extensión de List que deseamos mapear al ViewModel.</param>
//        /// <param name="types">Tipos internos específicos para mapear.</param>
//        /// <returns>Lista con el tipo de datos destino.</returns>
//        public static List<TDestination> ToMappedList<TSource, TDestination>(this List<TSource> list, List<KeyValuePair<Type, Type>> types)
//        {
//            return ToMappedList<TSource, TDestination>(list, types, null);
//        }

//        /// <summary>
//        /// Función que mapea automáticamente un objeto List de un tipo especificado a un List de otro tipo.
//        /// Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
//        /// </summary>
//        /// <typeparam name="TSource">Tipo de datos fuente.</typeparam>
//        /// <typeparam name="TDestination">Tipo de datos destino a donde le vamos a pasar la información desde el fuente.</typeparam>
//        /// <param name="list">Extensión de List que deseamos mapear al ViewModel.</param>
//        /// <param name="types">Tipos internos específicos para mapear.</param>
//        /// <param name="secuencial">Nombre de la propiedad sobre la que se aplicará un secuencial.</param>
//        /// <returns>Lista con el tipo de datos destino.</returns>
//        public static List<TDestination> ToMappedList<TSource, TDestination>(this List<TSource> list, List<KeyValuePair<Type, Type>> types, string secuencial)
//        {

//            // Creo las configuraciones del mapeo. Si un modelo tiene relación a un detalle de su modelo, este tipo de datos de su detalle 
//            // debe venir en la lista de tipos de datos.
//            var config = new MapperConfiguration(cfg =>
//            {
//                // Por definición creo el mapeo para el tipo de datos Origen y destino principales
//                cfg.CreateMap<TSource, TDestination>();

//                // En caso que el modelo tenga detalles, agrego los mapeos de los tipos de datos respectivos
//                if (types != null && types.Count > 0)
//                {
//                    foreach (var map in types)
//                    {
//                        cfg.CreateMap(map.Key, map.Value);
//                    }
//                }
//            });

//            var mapper = config.CreateMapper();

//            List<TDestination> result = mapper.Map<List<TSource>, List<TDestination>>(list);

//            // Verificar si se debe asigar un campo secuencial
//            if (!string.IsNullOrEmpty(secuencial))
//            {
//                if (result != null && result.Count > 0)
//                {
//                    TDestination registro;
//                    for (int cont = 0; cont < result.Count; cont++)
//                    {
//                        registro = result[cont];
//                        var propertyInfo = typeof(TDestination).GetProperties().Where(p => p.Name == secuencial).Select(p => p).FirstOrDefault();
//                        if (propertyInfo != null)
//                        {
//                            string nombreTipoDato = new GeneralFunctions().ObtenerNombreTipoDatoPropiedad(propertyInfo);
//                            if (nombreTipoDato.Equals("System.Int32") || nombreTipoDato.Equals("System.Decimal") || nombreTipoDato.Equals("System.Int64") || nombreTipoDato.Equals("System.Int16"))
//                                propertyInfo.SetValue(registro, cont + 1);
//                        }
//                    }
//                }
//            }

//            return result;
//        }


//        public static DataTable ToDataTable<T>(this List<T> lista)
//        {
//            DataTable dataTable = new DataTable(typeof(T).Name);

//            //Get all the properties
//            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
//            foreach (PropertyInfo prop in Props)
//            {
//                //Defining type of data column gives proper data table 
//                var type = (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ? Nullable.GetUnderlyingType(prop.PropertyType) : prop.PropertyType);
//                //Setting column names as Property names
//                dataTable.Columns.Add(prop.Name, type);
//            }
//            foreach (T item in lista)
//            {
//                var values = new object[Props.Length];
//                for (int i = 0; i < Props.Length; i++)
//                {
//                    //inserting property values to datatable rows
//                    values[i] = Props[i].GetValue(item, null);
//                }
//                dataTable.Rows.Add(values);
//            }
//            //put a breakpoint here and check datatable
//            return dataTable;
//        }

        
//    }
//}
