using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EciSupportCentre.Core.Presentation;

/// <summary>Turns the small HTML fragments of the news feed into plain text for a summary.</summary>
public static partial class HtmlText
{
    /// <summary>
    /// Removes tags, scripts and styles, decodes entities and tidies the white space.
    /// Paragraphs, line breaks and list items become single line breaks; empty lines are dropped.
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var text = Hidden().Replace(html, " ");
        text = Comment().Replace(text, " ");
        text = BlockEnd().Replace(text, "\n");
        text = ListItem().Replace(text, "\n• ");
        text = Tag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');

        var result = new StringBuilder(text.Length);
        foreach (var raw in text.Split('\n'))
        {
            var line = Spaces().Replace(raw, " ").Trim();
            if (line.Length > 0)
            {
                result.Append(result.Length > 0 ? "\n" : "").Append(line);
            }
        }

        return result.ToString();
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"<br\s*/?>|</(p|div|h[1-6]|ul|ol|li|tr|blockquote)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t\r\f\v]+")]
    private static partial Regex Spaces();
}
