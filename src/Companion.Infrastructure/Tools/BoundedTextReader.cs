using System.Globalization;
using System.Text;
using Companion.Application.Tools;

namespace Companion.Infrastructure.Tools;

/// <summary>Literal text decoding, shared with real-stream contract tests.</summary>
public static class BoundedTextReader
{
    public static async Task<string> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        var bytes = new byte[TextInspection.MaxBytes + 1];
        try
        {
            var count = 0;
            while (count < bytes.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await source.ReadAsync(bytes.AsMemory(count), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                count += read;
            }
            if (count > TextInspection.MaxBytes) throw new ToolException(ToolFailure.TooLarge);
            Encoding encoding = new UTF8Encoding(false, true);
            var skip = 0;
            if (count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) skip = 3;
            else if (count >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { encoding = new UnicodeEncoding(false, false, true); skip = 2; }
            else if (count >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { encoding = new UnicodeEncoding(true, false, true); skip = 2; }
            if (encoding.GetCharCount(bytes, skip, count - skip) > TextInspection.MaxCharacters) throw new ToolException(ToolFailure.TooLarge);
            var text = encoding.GetString(bytes, skip, count - skip);
            foreach (var rune in text.EnumerateRunes())
                if ((Rune.IsControl(rune) && rune.Value is not 9 and not 10 and not 13) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format)
                    throw new ToolException(ToolFailure.InvalidText);
            cancellationToken.ThrowIfCancellationRequested();
            return text;
        }
        catch (DecoderFallbackException) { throw new ToolException(ToolFailure.InvalidText); }
        finally { Array.Clear(bytes); }
    }
}
