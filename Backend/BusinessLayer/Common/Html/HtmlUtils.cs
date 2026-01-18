using Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace Academikus.AgenteInteligenteMentoresWebApi.Utility.General;

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
        result = System.Text.RegularExpressions.Regex.Replace(result, @">\s+<", "><");

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
}
