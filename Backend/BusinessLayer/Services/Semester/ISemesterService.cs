using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Semester;

/// <summary>
/// Servicio encargado de obtener información del semestre académico actual.
/// Provee métodos para consultar el semestre completo o únicamente su código.
/// </summary>
public interface ISemesterService
{
    /// <summary>
    /// Obtiene la información completa del semestre académico actual desde Salesforce.
    /// </summary>
    /// <returns>
    /// Un objeto <see cref="SemesterInfo"/> con los datos del semestre actual
    /// (nombre, código Banner, fechas, etc.),
    /// o <c>null</c> si no existe un semestre vigente o no se puede determinar.
    /// </returns>
    Task<SemesterInfo?> GetCurrentSemesterAsync();

    /// <summary>
    /// Obtiene únicamente el código del semestre académico actual.
    /// </summary>
    /// <returns>
    /// Una cadena con el código del semestre en formato <c>AAAAMM</c>
    /// (por ejemplo: <c>202401</c>),
    /// o <c>null</c> si no existe información del semestre.
    /// </returns>
    Task<string?> GetCurrentSemesterCodeAsync();
}
