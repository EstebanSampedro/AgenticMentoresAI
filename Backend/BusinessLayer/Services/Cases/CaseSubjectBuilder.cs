using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Cases;

/// <summary>
/// Utilidad para generar el asunto estándar de los casos de Salesforce
/// correspondientes al seguimiento académico.  
/// 
/// El formato esperado es:
/// <c>"Seguimiento Académico {SEMESTRE} -{NÚMERO} -{TEMA}"</c>.
/// 
/// Esta clase permite:
///  - Incrementar automáticamente el número de seguimiento.
///  - Reiniciar la numeración al cambiar de semestre.
///  - Detectar casos de apertura.
///  - Normalizar textos para análisis.
/// </summary>
public static class CaseSubjectBuilder
{
    /// <summary>
    /// Expresión regular utilizada para extraer el semestre, el número de seguimiento
    /// y el tema desde un asunto previamente generado.
    /// 
    /// Ejemplo válido:
    /// <c>"Seguimiento Académico 202401 -3 -Consultas generales"</c>
    /// </summary>
    private static readonly Regex SubjectRx = new(
        @"^Seguimiento\s+Acad(é|e)mico\s+(?<sem>\d{6})\s*-\s*(?<num>\d+)\s*-\s*(?<theme>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Genera el siguiente asunto de seguimiento académico, basado en el formato estándar
    /// y en el asunto previo registrado.
    /// </summary>
    /// <param name="previousSubject">
    /// Asunto anterior del caso.  
    /// Puede ser <c>null</c>, texto vacío o un asunto existente.
    /// </param>
    /// <param name="currentSemester">
    /// Código del semestre actual, en formato <c>AAAAMM</c>.
    /// </param>
    /// <param name="theme">
    /// Tema asociado al caso (por ejemplo: “Consultas generales”, “Justificación de faltas”).
    /// </param>
    /// <returns>
    /// Un asunto en formato:
    /// <c>"Seguimiento Académico {SEMESTRE} -{N} -{TEMA}"</c>,
    /// donde <c>N</c> es:
    /// <br/>
    /// • 1 si es un caso nuevo o si corresponde a apertura,<br/>
    /// • el consecutivo si el asunto previo pertenece al mismo semestre.
    /// </returns>
    public static string BuildNextSubject(string? previousSubject, string currentSemester, string theme)
    {
        // Caso de "Apertura de seguimiento" → siempre inicia con "1"
        if (IsOpeningCase(previousSubject))
            return $"Seguimiento Académico {currentSemester} -1 -{theme}";

        // Si existe un asunto previo en formato estándar, se intenta parsear
        if (!string.IsNullOrWhiteSpace(previousSubject))
        {
            var matchResult = SubjectRx.Match(previousSubject.Trim());

            // Si el formato coincide y el semestre es el mismo, se incrementa el contador
            if (matchResult.Success && string.Equals(matchResult.Groups["sem"].Value, currentSemester, StringComparison.Ordinal))
            {
                var previousSequenceNumber = int.Parse(
                    matchResult.Groups["num"].Value, 
                    CultureInfo.InvariantCulture
                );

                var nextSequenceNumber = previousSequenceNumber + 1;

                return $"Seguimiento Académico {currentSemester} -{nextSequenceNumber} -{theme}";
            }
        }

        // Si no se pudo parsear o el asunto pertenece a otro semestre,
        // se inicia el conteo desde 1
        return $"Seguimiento Académico {currentSemester} -1 -{theme}";
    }

    /// <summary>
    /// Determina si un asunto corresponde a un caso de apertura de seguimiento académico,
    /// el cual siempre reinicia la numeración del seguimiento a 1.
    /// </summary>
    /// <param name="s">Texto del asunto previo.</param>
    /// <returns>
    /// <c>true</c> si el asunto representa una apertura;
    /// de lo contrario, <c>false</c>.
    /// </returns>
    private static bool IsOpeningCase(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return false;

        var normalizedSubject = RemoveDiacritics(subject)
            .ToUpperInvariant()
            .Trim();

        return normalizedSubject == "APERTURA SEGUIMIENTO ACADEMICO"
            || normalizedSubject == "APERTURA DE SEGUIMIENTO ACADEMICO";
    }

    /// Elimina acentos y marcas diacríticas del texto para permitir comparaciones
    /// culturales y semánticas más estables.
    /// </summary>
    /// <param name="text">Texto original.</param>
    /// <returns>
    /// El texto sin acentos ni diacríticos, preservando el resto de caracteres.
    /// </returns>
    private static string RemoveDiacritics(string input)
    {
        var normalizedInput = input.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedInput.Length);

        foreach (var character in normalizedInput)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);

            // Agrega solo caracteres que no sean marcas diacríticas
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(character);
            }
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }
}
