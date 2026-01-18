namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Auth;

/// <summary>
/// Define operaciones para almacenar y recuperar tokens utilizados
/// en autenticación y acceso a servicios externos.
/// Esta interfaz permite implementar diferentes mecanismos de persistencia
/// como memoria, base de datos o caché distribuida.
/// </summary>
public interface ITokenStoreService
{
    /// <summary>
    /// Almacena un token asociado a una clave específica.
    /// </summary>
    /// <param name="key">
    /// Identificador único utilizado para recuperar el token posteriormente.
    /// </param>
    /// <param name="token">Token a almacenar.</param>
    Task StoreTokenAsync(string key, string token);

    /// <summary>
    /// Recupera un token previamente almacenado.
    /// </summary>
    /// <param name="key">Clave con la cual se almacenó el token.</param>
    /// <returns>
    /// Token encontrado o null si no existe.
    /// </returns>
    Task<string?> GetTokenAsync(string key);

    /// <summary>
    /// Obtiene un token desde la fuente de caché si existe y sigue siendo válido.
    /// Este método se utiliza cuando se quiere evitar una llamada externa
    /// innecesaria para generar un nuevo token.
    /// </summary>
    /// <param name="cacheKey">Clave única del token dentro del caché.</param>
    /// <returns>
    /// Token si está almacenado y vigente; en caso contrario null.
    /// </returns>
    Task<string?> GetCachedTokenAsync(string cacheKey);

    /// <summary>
    /// Guarda un token en caché con fecha de expiración, para soportar lógica
    /// de renovación y uso eficiente durante una ventana de tiempo establecida.
    /// </summary>
    /// <param name="cacheKey">Clave única del token en caché.</param>
    /// <param name="accessToken">Token a almacenar.</param>
    /// <param name="expiresOn">Fecha y hora UTC en que expira el token.</param>
    Task SaveTokenAsync(string cacheKey, string accessToken, DateTime expiresOn);
}
