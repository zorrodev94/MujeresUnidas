using System.Text.RegularExpressions;

namespace MujeresUnidas.Comunidad.Presentation;

/// <summary>
/// Safely interprets a deliberately small Markdown subset as native MAUI text spans.
/// Never executes HTML, CSS, scripts or arbitrary external links. ASCII emoticons are plain text.
/// </summary>
internal static class RichPostText
{
    private static readonly Regex Markup = new(
        @"\*\*(?<bold>[^*\r\n]+)\*\*|\*(?<italic>[^*\r\n]+)\*|~~(?<strike>[^\r\n]+)~~",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static FormattedString Parse(string text)
    {
        text ??= string.Empty;
        var result = new FormattedString();
        var offset = 0;
        foreach (Match match in Markup.Matches(text))
        {
            if (match.Index > offset)
                result.Spans.Add(new Span { Text = text[offset..match.Index] });
            var span = new Span
            {
                Text = match.Groups["bold"].Success ? match.Groups["bold"].Value :
                       match.Groups["italic"].Success ? match.Groups["italic"].Value : match.Groups["strike"].Value
            };
            if (match.Groups["bold"].Success) span.FontAttributes = FontAttributes.Bold;
            if (match.Groups["italic"].Success) span.FontAttributes = FontAttributes.Italic;
            if (match.Groups["strike"].Success) span.TextDecorations = TextDecorations.Strikethrough;
            result.Spans.Add(span);
            offset = match.Index + match.Length;
        }
        if (offset < text.Length) result.Spans.Add(new Span { Text = text[offset..] });
        if (result.Spans.Count == 0) result.Spans.Add(new Span { Text = string.Empty });
        return result;
    }

    internal static void Insert(Editor target, string prefix, string suffix, string placeholder)
    {
        var original = target.Text ?? string.Empty;
        var start = Math.Clamp(target.CursorPosition, 0, original.Length);
        var length = Math.Clamp(target.SelectionLength, 0, original.Length - start);
        var selected = length > 0 ? original.Substring(start, length) : placeholder;
        if (original.Length + prefix.Length + suffix.Length + selected.Length - length > target.MaxLength)
            return;
        target.Text = original.Remove(start, length).Insert(start, prefix + selected + suffix);
        target.CursorPosition = Math.Min(target.Text.Length, start + prefix.Length + selected.Length);
        target.SelectionLength = 0;
        target.Focus();
    }
}
