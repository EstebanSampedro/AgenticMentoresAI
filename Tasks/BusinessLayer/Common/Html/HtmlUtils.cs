using Academikus.AgenteInteligenteMentoresTareas.Business.Common;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace Academikus.AgenteInteligenteMentoresTareas.Utility.General;

public static class HtmlUtils
{
    public static HtmlProcessingResult ProcessHtmlAndStripImages(string html)
    {
        var result = new HtmlProcessingResult();

        if (string.IsNullOrWhiteSpace(html))
            return result;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Buscar <img> y recolectar sus src
        var imgNodes = doc.DocumentNode.SelectNodes("//img");
        if (imgNodes != null)
        {
            foreach (var img in imgNodes)
            {
                var src = img.GetAttributeValue("src", null);
                if (!string.IsNullOrEmpty(src))
                {
                    result.Urls.Add(src);
                }

                // Eliminar el nodo <img>
                img.Remove();
            }
        }

        // Añadir saltos de línea después de ciertas etiquetas
        var tagsToBreak = new[] { "br", "p", "div" };
        foreach (var node in doc.DocumentNode.Descendants().Where(n => tagsToBreak.Contains(n.Name.ToLower())).ToList())
        {
            node.ParentNode.InsertAfter(HtmlTextNode.CreateNode("\n"), node);
        }

        // Obtener texto plano y limpiar saltos de línea extra
        var plainText = doc.DocumentNode.InnerText;
        var decodedText = HtmlEntity.DeEntitize(plainText);
        result.CleanText = Regex.Replace(decodedText, @"\n\s*\n", "\n").Trim();

        return result;
    }

    // Con validación de contenido vacío
    public static string ProcessHtmlAndStripAttachmentsRobust(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Eliminar <attachment>
        var attachmentNodes = doc.DocumentNode.SelectNodes("//attachment");
        if (attachmentNodes != null)
        {
            foreach (var node in attachmentNodes)
            {
                node.Remove();
            }
        }

        // Eliminar elementos completamente vacíos o solo con espacios ANTES de procesar
        var emptyElements = doc.DocumentNode.Descendants()
            .Where(n => n.NodeType == HtmlNodeType.Element &&
                       (n.Name.ToLower() == "p" || n.Name.ToLower() == "div") &&
                       IsElementEmpty(n))
            .ToList();

        foreach (var emptyElement in emptyElements)
        {
            emptyElement.Remove();
        }

        // Obtener elementos de bloque de nivel superior que tienen contenido real
        var blockElements = doc.DocumentNode.ChildNodes
            .Where(n => n.NodeType == HtmlNodeType.Element &&
                       (n.Name.ToLower() == "p" || n.Name.ToLower() == "div") &&
                       !IsElementEmpty(n))
            .ToList();

        // Si no hay elementos con contenido, retornar vacío
        if (!blockElements.Any())
        {
            return string.Empty;
        }

        // Agregar <br/> solo entre elementos (no después del último)
        for (int i = 0; i < blockElements.Count - 1; i++)
        {
            var currentElement = blockElements[i];

            // Solo agregar <br/> si ambos elementos están al mismo nivel
            if (currentElement.ParentNode == blockElements[i + 1].ParentNode)
            {
                var breakNode = HtmlNode.CreateNode("<br/>");
                currentElement.ParentNode.InsertAfter(breakNode, currentElement);
            }
        }

        // Limpiar espacios extra y retornar
        var result = doc.DocumentNode.InnerHtml.Trim();

        // Limpiar espacios múltiples entre tags
        result = Regex.Replace(result, @">\s+<", "><");

        return result;
    }

    // MÉTODO HELPER: Determinar si un elemento está vacío
    private static bool IsElementEmpty(HtmlNode element)
    {
        if (element == null) return true;

        // Obtener el texto sin HTML
        var textContent = element.InnerText ?? string.Empty;

        // Decodificar entidades HTML (&nbsp;, etc.)
        textContent = System.Web.HttpUtility.HtmlDecode(textContent);

        // Limpiar espacios en blanco, tabs, saltos de línea
        textContent = System.Text.RegularExpressions.Regex.Replace(textContent, @"\s+", " ").Trim();

        // Considerar vacío si no hay texto o solo contiene espacios
        return string.IsNullOrEmpty(textContent);
    }

    /// <summary>
    /// Reescribe las URLs de imágenes de Graph para que sean servidas desde el backend,
    /// reemplazando los enlaces de Microsoft Graph por enlaces internos accesibles por la UI.
    /// </summary>
    /// <remarks>
    /// Ejemplo de URL original:
    /// https://graph.microsoft.com/v1.0/chats/{chatId}/messages/{messageId}/hostedContents/{hostedId}/$value
    ///
    /// Se transforma en:
    /// {baseUrl}/api/images/{chatId}/{messageId}/{hostedId}
    /// </remarks>
    /// <param name="html">Contenido HTML del mensaje proveniente de Graph.</param>
    /// <returns>HTML modificado con URLs internas accesibles desde el sistema.</returns>
    public static string RewriteGraphImageUrls(string html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        // Patrón que identifica imágenes provenientes de Microsoft Graph
        string pattern = @"https://graph\.microsoft\.com/v1\.0/chats/(?<chatId>[^/]+)/messages/(?<messageId>[^/]+)/hostedContents/(?<hostedId>[^/]+)/\$value";

        var baseUrl = EnvironmentHelper.GetBackendBaseUrl();

        string updatedHtml = Regex.Replace(html, pattern, match =>
        {
            try
            {
                // Extraemos los valores limpios usando los nombres de grupo
                string chatId = match.Groups["chatId"].Value;
                string messageId = match.Groups["messageId"].Value;
                string hostedId = match.Groups["hostedId"].Value;

                // Construimos la nueva URL interna
                // IMPORTANTE: Usamos EscapeDataString por si los IDs tienen caracteres especiales
                return $"{baseUrl}/api/images/" +
                       $"{Uri.EscapeDataString(chatId)}/" +
                       $"{Uri.EscapeDataString(messageId)}/" +
                       $"{Uri.EscapeDataString(hostedId)}";
            }
            catch
            {
                // Si algo falla en el reemplazo, devolvemos la URL original (fail-safe)
                return match.Value;
            }
        });

        return updatedHtml;
    }

    public static string BuildJustificationEmailHtml(
        string studentEmail,
        string bannerId,
        string career,
        string identification,
        string studentFullName,
        string certificateType,
        string dateInit,
        string dateEnd,
        string summary,
        string analysis)
    {
        string safe(string? v) => string.IsNullOrWhiteSpace(v) ? "-" : System.Net.WebUtility.HtmlEncode(v);

        // Si las fechas vienen en diferentes formatos, aquí podrías normalizarlas si lo deseas
        var html = $@"
            <!DOCTYPE html>
            <html>
              <head>
                <meta charset=""utf-8"" />
                <title>Justificación de Faltas</title>
              </head>
              <body>

                <h3>Datos del Estudiante</h3>
                <table border=""1"" cellpadding=""6"" cellspacing=""0"" style=""border-collapse:collapse;"">
                  <tr><th align=""left"">Nombre</th><td>{safe(studentFullName)}</td></tr>
                  <tr><th align=""left"">Correo</th><td>{safe(studentEmail)}</td></tr>
                  <tr><th align=""left"">ID Banner</th><td>{safe(bannerId)}</td></tr>
                  <tr><th align=""left"">Carrera</th><td>{safe(career)}</td></tr>
                  <tr><th align=""left"">Identificación</th><td>{safe(identification)}</td></tr>
                </table>

                <h3>Datos del Certificado</h3>
                <table border=""1"" cellpadding=""6"" cellspacing=""0"" style=""border-collapse:collapse;"">
                  <tr><th align=""left"">Tipo</th><td>{safe(certificateType)}</td></tr>
                  <tr><th align=""left"">Inicio</th><td>{safe(dateInit)}</td></tr>
                  <tr><th align=""left"">Fin</th><td>{safe(dateEnd)}</td></tr>
                  <tr><th align=""left"">Resumen</th><td>{safe(summary)}</td></tr>
                  <tr><th align=""left"">Análisis</th><td>{safe(analysis)}</td></tr>
                </table>

              </body>
            </html>";

        return html;
    }
}
