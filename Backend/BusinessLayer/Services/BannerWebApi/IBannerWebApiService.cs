using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.BannerWebApi;

/// <summary>
/// Define los servicios necesarios para interactuar con la Banner Web API,
/// incluyendo obtención de token, información básica de estudiantes
/// y datos clave institucionales.
/// </summary>
public interface IBannerWebApiService
{
    /// <summary>
    /// Consulta en BannerWebApi información básica académica y administrativa
    /// asociada al correo institucional del estudiante.
    /// </summary>
    /// <param name="request">
    /// Contiene el correo institucional del estudiante requerido para la consulta.
    /// </param>
    /// <returns>
    /// Un objeto <see cref="BasicInformationResponse"/> con la información obtenida,
    /// o null si la llamada falla o la respuesta es inválida.
    /// </returns>
    Task<BasicInformationResponse?> GetBasicInformationAsync(BasicInformationRequest request);

    /// <summary>
    /// Obtiene datos clave del estudiante como identificadores internos, BannerId, PIDM
    /// y la lista de programas académicos registrados.
    /// </summary>
    /// <param name="institutionalEmail">Correo institucional del estudiante.</param>
    /// <returns>
    /// Una tupla con PersonId, BannerId, Pidm y una lista de descripciones de programas.
    /// Si no existe información o ocurre un error, todos los valores se devuelven vacíos.
    /// </returns>
    Task<(string PersonId, string BannerId, string Pidm, List<string> ProgramDescriptions)> 
        GetStudentKeyInfoAsync(string institutionalEmail);
}
