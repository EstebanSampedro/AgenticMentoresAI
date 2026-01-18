namespace Academikus.AgenteInteligenteMentoresTareas.Business.Common;

/// <summary>
/// Utilidades para validar y normalizar tipos de certificados
/// recibidos desde la IA u otras fuentes externas.
/// </summary>
public static class CertificateHelper
{
    /// <summary>
    /// Lista de tipos de certificados permitidos en el sistema.
    /// Comparación insensible a mayúsculas y minúsculas.
    /// </summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "CitaMedicaSinReposo",
        "CitaMedicaConReposo",
        "CitaMedicaHijosMenores",
        "RepresentacionUniversitaria",
        "CalamidadDomestica",
        "Desconocido"
    };

    /// <summary>
    /// Valida si el certificado pertenece al listado permitido.
    /// Se normaliza primero para corregir errores de formato.
    /// </summary>
    /// <param name="certificate">Texto a evaluar.</param>
    /// <returns>True si el certificado es permitido.</returns>
    public static bool IsAllowed(string? certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate))
        {
            Console.WriteLine("[Certificate] Certificado vacío o nulo.");
            return false;
        }

        var normalized = Normalize(certificate);
        var result = Allowed.Contains(normalized);

        return result;
    }

    /// <summary>
    /// Normaliza texto eliminando espacios, guiones, tildes y minúsculas,
    /// mapeándolo a los valores oficiales del sistema.
    /// </summary>
    public static string Normalize(string certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate))
            return "Desconocido";

        // Limpieza común de entrada
        var raw = certificate
            .Trim()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace("’", "")
            .Replace("'", "")
            .Replace("ñ", "n").Replace("Ñ", "N")
            .Replace("á", "a").Replace("Á", "A")
            .Replace("é", "e").Replace("É", "E")
            .Replace("í", "i").Replace("Í", "I")
            .Replace("ó", "o").Replace("Ó", "O")
            .Replace("ú", "u").Replace("Ú", "U");

        var key = raw.ToLowerInvariant();

        // Mapeo a nombres oficiales del sistema
        return key switch
        {
            "citamedicaconreposo" => "CitaMedicaConReposo",
            "citamedicasinreposo" => "CitaMedicaSinReposo",
            "citamedicahijosmenores" => "CitaMedicaHijosMenores",
            "representacionuniversitaria" => "RepresentacionUniversitaria",
            "calamidaddomestica" or
            "calamidad" or
            "domestica" => "CalamidadDomestica",
            "desconocido" => "Desconocido",

            // Si no coincide → lo marcamos como desconocido
            _ => "Desconocido"
        };
    }
}
