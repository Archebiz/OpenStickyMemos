using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace OpenStickyMemos.Desktop.Services;

/// <summary>
/// Convierte entre HTML simple (negrita, cursiva, subrayado y viñetas) y FlowDocument,
/// para el editor enriquecido de las notas del desktop. Los tags soportados son:
/// <b>/<strong>, <i>/<em>, <u>, <br/>, <ul><li>…</li></ul> y <p>…</p>.
/// </summary>
public static class RichTextConverter
{
    // ── FlowDocument → HTML ──

    public static string ToHtml(FlowDocument doc)
    {
        if (doc is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var block in doc.Blocks)
        {
            switch (block)
            {
                case Paragraph p:
                    WriteParagraph(sb, p);
                    break;
                case List list:
                    WriteList(sb, list);
                    break;
            }
        }
        return Normalize(sb.ToString());
    }

    /// <summary>
    /// Normaliza el HTML resultante: contenido esencialmente vacío (párrafos vacíos)
    /// se devuelve como cadena vacía.
    /// </summary>
    public static string Normalize(string? html)
    {
        var trimmed = (html ?? string.Empty).Trim();
        if (trimmed.Length == 0
            || trimmed == "<p></p>"
            || trimmed == "<p><br/></p>"
            || trimmed == "<p><br></p>"
            || trimmed == "<br/>"
            || trimmed == "<br>") return string.Empty;
        return trimmed;
    }

    private static void WriteParagraph(StringBuilder sb, Paragraph p)
    {
        sb.Append("<p>");
        WriteInlines(sb, p.Inlines);
        sb.Append("</p>");
    }

    private static void WriteList(StringBuilder sb, List list)
    {
        sb.Append("<ul>");
        foreach (var item in list.ListItems)
        {
            sb.Append("<li>");
            foreach (var block in item.Blocks)
            {
                if (block is Paragraph p)
                    WriteInlines(sb, p.Inlines);
            }
            sb.Append("</li>");
        }
        sb.Append("</ul>");
    }

    private static void WriteInlines(StringBuilder sb, InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LineBreak:
                    sb.Append("<br/>");
                    break;
                case Run run:
                    WriteRun(sb, run);
                    break;
                case Span span:
                    WriteSpan(sb, span);
                    break;
            }
        }
    }

    private static void WriteSpan(StringBuilder sb, Span span)
    {
        var bold = span.FontWeight == FontWeights.Bold;
        var italic = span.FontStyle == FontStyles.Italic;
        var underline = HasUnderline(span.TextDecorations);

        AppendStartTags(sb, bold, italic, underline);
        WriteInlines(sb, span.Inlines);
        AppendEndTags(sb, bold, italic, underline);
    }

    private static void WriteRun(StringBuilder sb, Run run)
    {
        var bold = run.FontWeight == FontWeights.Bold;
        var italic = run.FontStyle == FontStyles.Italic;
        var underline = HasUnderline(run.TextDecorations);

        AppendStartTags(sb, bold, italic, underline);
        sb.Append(System.Net.WebUtility.HtmlEncode(run.Text));
        AppendEndTags(sb, bold, italic, underline);
    }

    private static bool HasUnderline(TextDecorationCollection? decorations)
    {
        if (decorations is null) return false;
        foreach (var d in decorations)
        {
            if (d.Location == TextDecorationLocation.Underline)
                return true;
        }
        return false;
    }

    private static void AppendStartTags(StringBuilder sb, bool bold, bool italic, bool underline)
    {
        if (bold) sb.Append("<b>");
        if (italic) sb.Append("<i>");
        if (underline) sb.Append("<u>");
    }

    private static void AppendEndTags(StringBuilder sb, bool bold, bool italic, bool underline)
    {
        if (underline) sb.Append("</u>");
        if (italic) sb.Append("</i>");
        if (bold) sb.Append("</b>");
    }

    // ── HTML → FlowDocument ──

    public static FlowDocument ToFlowDocument(string? html)
    {
        var doc = new FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12,
            LineHeight = 1.1,
            PagePadding = new Thickness(0)
        };
        doc.Blocks.Clear();
        ParseBlocks(doc, html ?? string.Empty);
        return doc;
    }

    private static void ParseBlocks(FlowDocument doc, string html)
    {
        int pos = 0;
        while (pos < html.Length)
        {
            var pStart = html.IndexOf("<p", pos, System.StringComparison.OrdinalIgnoreCase);
            var ulStart = html.IndexOf("<ul", pos, System.StringComparison.OrdinalIgnoreCase);

            int blockStart;
            string blockTag;
            if (pStart >= 0 && (ulStart < 0 || pStart < ulStart))
            {
                blockStart = pStart;
                blockTag = "p";
            }
            else if (ulStart >= 0)
            {
                blockStart = ulStart;
                blockTag = "ul";
            }
            else
            {
                blockStart = -1;
                blockTag = string.Empty;
            }

            if (blockStart < 0)
            {
                var rest = html.Substring(pos);
                if (!string.IsNullOrWhiteSpace(rest))
                    doc.Blocks.Add(ParagraphFromText(rest));
                break;
            }

            var loose = html.Substring(pos, blockStart - pos);
            if (!string.IsNullOrWhiteSpace(loose))
                doc.Blocks.Add(ParagraphFromText(loose));

            if (blockTag == "p")
            {
                var contentStart = html.IndexOf('>', blockStart) + 1;
                var close = html.IndexOf("</p", blockStart, System.StringComparison.OrdinalIgnoreCase);
                var inner = close >= 0
                    ? html.Substring(contentStart, close - contentStart)
                    : html.Substring(contentStart);
                var para = new Paragraph();
                ParseInline(para.Inlines, inner);
                doc.Blocks.Add(para);
                pos = close >= 0 ? html.IndexOf('>', close) + 1 : html.Length;
            }
            else if (blockTag == "ul")
            {
                var contentStart = html.IndexOf('>', blockStart) + 1;
                var close = html.IndexOf("</ul", blockStart, System.StringComparison.OrdinalIgnoreCase);
                var inner = close >= 0
                    ? html.Substring(contentStart, close - contentStart)
                    : html.Substring(contentStart);
                var list = new List();
                ParseListItems(list, inner);
                if (list.ListItems.Count > 0)
                    doc.Blocks.Add(list);
                pos = close >= 0 ? html.IndexOf('>', close) + 1 : html.Length;
            }
            else
            {
                break;
            }
        }
    }

    private static Paragraph ParagraphFromText(string html)
    {
        var para = new Paragraph();
        ParseInline(para.Inlines, html);
        return para;
    }

    private static void ParseListItems(List list, string inner)
    {
        int pos = 0;
        while (pos < inner.Length)
        {
            var liStart = inner.IndexOf("<li", pos, System.StringComparison.OrdinalIgnoreCase);
            if (liStart < 0) break;

            var contentStart = inner.IndexOf('>', liStart) + 1;
            var liEnd = inner.IndexOf("</li", contentStart, System.StringComparison.OrdinalIgnoreCase);
            var content = liEnd >= 0
                ? inner.Substring(contentStart, liEnd - contentStart)
                : inner.Substring(contentStart);

            var item = new ListItem();
            var para = new Paragraph();
            ParseInline(para.Inlines, content);
            item.Blocks.Add(para);
            list.ListItems.Add(item);

            if (liEnd < 0) break;
            pos = inner.IndexOf('>', liEnd) + 1;
        }
    }

    /// <summary>
    /// Parsea contenido inline: texto con <b>/<i>/<u>/<br/>. Los flags se acumulan
    /// mientras un tag está abierto (el HTML generado por nosotros siempre anida bien).
    /// </summary>
    private static void ParseInline(InlineCollection inlines, string html)
    {
        bool bold = false, italic = false, underline = false;
        int pos = 0;
        while (pos < html.Length)
        {
            var lt = html.IndexOf('<', pos);
            if (lt < 0)
            {
                AddRun(inlines, html.Substring(pos), bold, italic, underline);
                break;
            }

            var text = html.Substring(pos, lt - pos);
            AddRun(inlines, text, bold, italic, underline);

            var gt = html.IndexOf('>', lt);
            if (gt < 0) break;
            var tag = html.Substring(lt + 1, gt - lt - 1).Trim().ToLowerInvariant();
            pos = gt + 1;

            switch (tag)
            {
                case "b":
                case "strong":
                    bold = true;
                    break;
                case "/b":
                case "/strong":
                    bold = false;
                    break;
                case "i":
                case "em":
                    italic = true;
                    break;
                case "/i":
                case "/em":
                    italic = false;
                    break;
                case "u":
                    underline = true;
                    break;
                case "/u":
                    underline = false;
                    break;
                case "br":
                case "br/":
                case "br /":
                    inlines.Add(new LineBreak());
                    break;
            }
        }
    }

    private static void AddRun(InlineCollection inlines, string text, bool bold, bool italic, bool underline)
    {
        text = System.Net.WebUtility.HtmlDecode(text);
        if (text.Length == 0) return;

        // Preservar saltos de línea del texto plano (notas creadas antes del editor rico)
        var segments = text.Split('\n');
        for (int i = 0; i < segments.Length; i++)
        {
            if (i > 0)
                inlines.Add(new LineBreak());

            var seg = segments[i].TrimEnd('\r');
            if (seg.Length == 0) continue;

            var run = new Run(seg);
            if (bold) run.FontWeight = FontWeights.Bold;
            if (italic) run.FontStyle = FontStyles.Italic;
            if (underline) run.TextDecorations = TextDecorations.Underline;
            inlines.Add(run);
        }
    }
}
