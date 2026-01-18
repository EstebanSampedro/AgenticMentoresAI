using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;

namespace Academikus.AgenteInteligenteMentoresWebApi.Utility.Mapper
{

    /// <summary>
    /// Extensión para los Modelos que nos permite hacer un AutoMap desde la entidad creada en Entity Framework a nuestro modelo predefinido.
    /// </summary>
    public static class ModelExtension
    {

        /// <summary>
        /// Función que mapea los valores de un modelo a otro. Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
        /// </summary>
        /// <typeparam name="TSource">Tipo de datos del Modelo fuente que contiene la información consultada desde la Base de datos. Generalemente es el modelo de la entidad.</typeparam>
        /// <typeparam name="TDestination">Tipo de datos del modelo destino a donde le vamos a pasar la información desde el modelo fuente. Generalmente es una vista modelo de la entidad.</typeparam>
        /// <param name="model">Modelo fuente que contiene los datos a ser mapeados.</param>
        /// <returns>Devuelve el modelo resultante que contien los datos mapeados en el modelo fuente.</returns>
        public static TDestination ToMapped<TSource, TDestination>(this TSource model)
        {
            return ToMapped<TSource, TDestination>(model, null, null);
        }

        /// <summary>
        /// Función que mapea los valores de un modelo a otro. Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
        /// </summary>
        /// <typeparam name="TSource">Tipo de datos del Modelo fuente que contiene la información consultada desde la Base de datos. Generalemente es el modelo de la entidad.</typeparam>
        /// <typeparam name="TDestination">Tipo de datos del modelo destino a donde le vamos a pasar la información desde el modelo fuente. Generalmente es una vista modelo de la entidad.</typeparam>
        /// <param name="model">Modelo fuente que contiene los datos a ser mapeados.</param>
        /// <param name="types">Lista KeyValuePair de los tipos de datos detalles del modelo principal. Son los tipos de datos que también deben ser mapeados.</param>
        /// <returns>Devuelve el modelo resultante que contien los datos mapeados en el modelo fuente.</returns>
        public static TDestination ToMapped<TSource, TDestination>(this TSource model, List<KeyValuePair<Type, Type>> types)
        {
            return ToMapped<TSource, TDestination>(model, types, null);
        }


        /// <summary>
        /// Función que mapea los valores de un modelo a otro. Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
        /// </summary>
        /// <typeparam name="TSource">Tipo de datos del Modelo fuente que contiene la información consultada desde la Base de datos. Generalemente es el modelo de la entidad.</typeparam>
        /// <typeparam name="TDestination">Tipo de datos del modelo destino a donde le vamos a pasar la información desde el modelo fuente. Generalmente es una vista modelo de la entidad.</typeparam>
        /// <param name="model">Modelo fuente que contiene los datos a ser mapeados.</param>
        /// <param name="ignoredProperties">En caso que el modelo tenga detalles y no se quiera obtener el valor de estos, se debe especificar en esta lista el nombre de las propiedades detalles a ser ignoradas.</param>
        /// <returns>Devuelve el modelo resultante que contien los datos mapeados en el modelo fuente.</returns>
        public static TDestination ToMapped<TSource, TDestination>(this TSource model, List<string> ignoredProperties)
        {
            return ToMapped<TSource, TDestination>(model, null, ignoredProperties);
        }


        /// <summary>
        /// Función que mapea los valores de un modelo a otro. Los modelos deben tener las propiedades con los mismo nombres para que pueda hacer el mapeo correctamente.
        /// </summary>
        /// <typeparam name="TSource">Tipo de datos del Modelo fuente que contiene la información consultada desde la Base de datos. Generalemente es el modelo de la entidad.</typeparam>
        /// <typeparam name="TDestination">Tipo de datos del modelo destino a donde le vamos a pasar la información desde el modelo fuente. Generalmente es una vista modelo de la entidad.</typeparam>
        /// <param name="model">Modelo fuente que contiene los datos a ser mapeados.</param>
        /// <param name="types">Lista KeyValuePair de los tipos de datos detalles del modelo principal. Son los tipos de datos que también deben ser mapeados.</param>
        /// <param name="ignoredProperties">En caso que el modelo tenga detalles y no se quiera obtener el valor de estos, se debe especificar en esta lista el nombre de las propiedades detalles a ser ignoradas.</param>
        /// <returns>Devuelve el modelo resultante que contien los datos mapeados en el modelo fuente.</returns>
        public static TDestination ToMapped<TSource, TDestination>(this TSource model, List<KeyValuePair<Type, Type>> types, List<string> ignoredProperties)
        {

            // Creo las configuraciones del mapeo. Si un modelo tiene relación a un detalle de su modelo, este tipo de datos de su detalle 
            // debe venir en la lista de tipos de datos.
            var config = new MapperConfiguration(cfg =>
            {
                if (ignoredProperties != null && ignoredProperties.Count > 0)
                {
                    foreach (var item in ignoredProperties)
                    {
                        cfg.AddGlobalIgnore(item);
                    }

                }

                // Por definición creo el mapeo para el tipo de datos Origen y destino principales
                cfg.CreateMap<TSource, TDestination>();

                // En caso que el modelo tenga detalles, agrego los mapeos de los tipos de datos respectivos
                if (types != null && types.Count > 0)
                {
                    foreach (var map in types)
                    {
                        cfg.CreateMap(map.Key, map.Value);
                    }
                }
            });

            var mapper = config.CreateMapper();
            TDestination resultModel = mapper.Map<TSource, TDestination>(model);
            return resultModel;

        }

    }
}
