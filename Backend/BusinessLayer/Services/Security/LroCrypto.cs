using Microsoft.AspNetCore.DataProtection;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Security;

/// <summary>
/// Proporciona funcionalidad de protección y desprotección de las sessionKeys utilizadas
/// en las sesiones de larga duración (LRO) dentro del flujo Long-Running OBO.
/// 
/// Esta clase encapsula la lógica de cifrado utilizando <see cref="IDataProtectionProvider"/>,
/// asegurando que las sessionKeys se almacenen cifradas en base de datos y solo puedan ser
/// recuperadas por la misma aplicación u otras instancias autorizadas.
/// </summary>
public sealed class LroCrypto
{
    private readonly IDataProtector _protector;

    /// <summary>
    /// Inicializa una nueva instancia del protector criptográfico para sessionKeys.
    /// </summary>
    /// <param name="dp">
    /// Proveedor de protección de datos utilizado para generar el protector que cifra
    /// y descifra sessionKeys de forma segura. 
    /// </param>
    /// <remarks>
    /// La cadena "LroSessionKey" se utiliza como propósito, garantizando que las claves generadas
    /// sean específicas para la protección de sessionKeys y no se compartan con otros propósitos.
    /// </remarks>
    public LroCrypto(IDataProtectionProvider dp) 
        => _protector = dp.CreateProtector("LroSessionKey");

    /// <summary>
    /// Cifra una sessionKey en texto plano y la devuelve como un arreglo de bytes.
    /// </summary>
    /// <param name="sessionKeyPlain">SessionKey sin proteger que se va a cifrar.</param>
    /// <returns>
    /// Un arreglo de bytes que contiene la representación cifrada de la sessionKey.
    /// Este valor es adecuado para almacenamiento persistente.
    /// </returns>
    /// <remarks>
    /// La sessionKey se cifra utilizando el protector interno y luego se convierte a UTF-8
    /// para permitir su almacenamiento como <c>varbinary</c> en la base de datos.
    /// </remarks>
    public byte[] Protect(string sessionKeyPlain)
        => Encoding.UTF8.GetBytes(_protector.Protect(sessionKeyPlain));

    /// <summary>
    /// Descifra una sessionKey previamente protegida y la devuelve como texto plano.
    /// </summary>
    /// <param name="cipher">
    /// Arreglo de bytes que contiene la sessionKey cifrada obtenida desde la base de datos.
    /// </param>
    /// <returns>
    /// La sessionKey en texto plano, lista para ser utilizada en <c>AcquireTokenInLongRunningProcess</c>.
    /// </returns>
    /// <remarks>
    /// Este método ejecuta la operación inversa de <see cref="Protect(string)"/>.
    /// Primero convierte los bytes a texto UTF-8 y luego utiliza el protector para descifrar el valor.
    /// </remarks>
    public string Unprotect(byte[] cipher)
        => _protector.Unprotect(Encoding.UTF8.GetString(cipher));
}
