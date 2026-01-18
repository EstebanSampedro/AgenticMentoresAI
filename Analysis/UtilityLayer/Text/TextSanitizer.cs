// ReSharper disable StringLiteralTypo
namespace Academikus.AnalysisMentoresVerdes.Utility.Text;

using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Utilidades para limpiar HTML a texto plano y anonimizar datos sensibles.
/// Sin dependencias externas (solo Regex + WebUtility).
/// </summary>
public static class TextSanitizer
{
    // ---------- Regex pre-compilados ----------
    private static readonly Regex Ws = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex BrTag = new(@"<(br|BR)\s*/?>", RegexOptions.Compiled);
    private static readonly Regex PEndTag = new(@"</p\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ScriptBlock = new(@"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StyleBlock = new(@"<style\b[^<]*(?:(?!</style>)<[^<]*)*</style>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AnyTag = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex UrlRe = new(@"\bhttps?://[^\s<>\)]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EmailRe = new(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRe = new(@"\b(?:\+?\d[\s\-\(\)]*){8,}\d\b", RegexOptions.Compiled); // teléfonos comunes
    private static readonly Regex LongNumberRe = new(@"\b\d{8,}\b", RegexOptions.Compiled); // IDs largos, códigos
    private static readonly Regex MentionRe = new(@"(?<!\w)@[\p{L}\d\.\-_]{2,}", RegexOptions.Compiled); // @usuario

    /// <summary>
    /// Convierte HTML o texto enriquecido a texto plano legible.
    /// - Quita &lt;script&gt; y &lt;style&gt;.
    /// - Convierte &lt;br&gt; y fin de &lt;p&gt; en saltos de línea.
    /// - Elimina el resto de etiquetas.
    /// - Decodifica entidades HTML.
    /// - Normaliza espacios.
    /// </summary>
    public static string ToPlainText(string? htmlOrText)
    {
        if (string.IsNullOrWhiteSpace(htmlOrText))
            return string.Empty;

        var s = htmlOrText;

        // Preserva saltos de línea donde tiene sentido
        s = BrTag.Replace(s, "\n");
        s = PEndTag.Replace(s, "\n");

        // Remueve bloques no visibles
        s = ScriptBlock.Replace(s, " ");
        s = StyleBlock.Replace(s, " ");

        // Elimina cualquier etiqueta restante
        s = AnyTag.Replace(s, " ");

        // Decodifica entidades (&amp; &lt; &gt; &quot; etc.)
        s = WebUtility.HtmlDecode(s);

        // Normaliza espacios
        s = Ws.Replace(s, " ").Trim();

        return s;
    }

    /// <summary>
    /// Anonimiza datos sensibles en texto plano:
    /// URLs, emails, teléfonos, números largos (IDs), menciones @usuario,
    /// y opcionalmente nombres adicionales provistos.
    /// </summary>
    public static string Anonymize(string? plainText, IEnumerable<string>? extraNames = null)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return string.Empty;

        var s = plainText;

        s = UrlRe.Replace(s, "[URL]");
        s = EmailRe.Replace(s, "[EMAIL]");
        s = PhoneRe.Replace(s, "[PHONE]");
        s = LongNumberRe.Replace(s, "[ID]");
        s = MentionRe.Replace(s, "[MENTION]");

        if (extraNames is not null)
        {
            foreach (var name in extraNames.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var pattern = Regex.Escape(name.Trim());
                // Reemplazo respetando palabra completa, sin sensibilidad a mayúsculas
                s = Regex.Replace(s, $@"\b{pattern}\b", "[NAME]", RegexOptions.IgnoreCase);
            }
        }

        s = Ws.Replace(s, " ").Trim();
        return s;
    }

    /// <summary>
    /// Limpia HTML y anonimiza en un solo paso.
    /// </summary>
    public static string Sanitize(string? htmlOrText, IEnumerable<string>? extraNames = null)
        => Anonymize(ToPlainText(htmlOrText), extraNames);
}
