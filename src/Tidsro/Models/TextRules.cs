using System.Globalization;

namespace Tidsro.Models;

public static class TextRules
{
    /// <summary>Cut <paramref name="text"/> to at most <paramref name="max"/> UTF-16 code units, at a
    /// text-element boundary, so an emoji (a surrogate pair, or several joined into one) is kept whole
    /// or dropped whole, never split.</summary>
    public static string Cap(string text, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        if (text.Length <= max) return text;

        var cut = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var end = elements.ElementIndex + elements.GetTextElement().Length;
            if (end > max) break;
            cut = end;
        }

        // One text element longer than the cap (a letter under hundreds of combining marks) leaves
        // nothing to keep. Fall back to a plain cut that still never ends on half a surrogate pair.
        if (cut == 0) cut = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..cut];
    }
}
