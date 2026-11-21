using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SWTORCombatParser.Model.LogParsing;

internal readonly record struct PhysicalLogLine(string Text, long EndOffset);

internal static class PhysicalLogReader
{
    // cp1252, UTF-8 and BOM-detected UTF-16/32. Match LF on a code-unit boundary.
    internal static List<PhysicalLogLine> Read(Stream source, long offset, Encoding encoding)
    {
        source.Position = offset;
        var lines = new List<PhysicalLogLine>();
        long remaining = source.Length - offset;
        if (remaining <= 0) return lines;
        var newline = encoding.GetBytes("\n");
        int width = newline.Length;
        int lfIndex = Array.IndexOf(newline, (byte)10);
        var buffer = new byte[16384];
        using var partial = new MemoryStream();
        while (remaining > 0)
        {
            int size = (int)Math.Min(buffer.Length, remaining);
            int read = source.ReadAtLeast(buffer.AsSpan(0, size), size, throwOnEndOfStream: false);
            if (read == 0) break;
            int start = 0;
            int search = lfIndex;
            while (search < read)
            {
                int lf = Array.IndexOf(buffer, (byte)10, search, read - search);
                if (lf < 0) break;
                search = lf + 1;
                int end = lf - lfIndex;
                if (end < start || end % width != 0 || end + width > read ||
                    !buffer.AsSpan(end, width).SequenceEqual(newline)) continue;
                string text;
                if (partial.Length == 0)
                    text = encoding.GetString(buffer, start, end - start);
                else
                {
                    partial.Write(buffer, start, end - start);
                    text = encoding.GetString(partial.GetBuffer(), 0, checked((int)partial.Length));
                    partial.SetLength(0);
                }
                if (text.StartsWith('\0')) text = text.TrimStart('\0');
                if (text.EndsWith('\r')) text = text[..^1];
                start = end + width;
                lines.Add(new PhysicalLogLine(text, offset + start));
            }
            partial.Write(buffer, start, read - start);
            offset += read;
            remaining -= read;
        }
        return lines;
    }

    internal static long DetectPreamble(StreamReader reader)
    {
        reader.Peek();
        var stream = reader.BaseStream;
        stream.Position = 0;
        var preamble = reader.CurrentEncoding.GetPreamble();
        Span<byte> prefix = stackalloc byte[preamble.Length];
        return stream.ReadAtLeast(prefix, prefix.Length, false) == prefix.Length &&
            prefix.SequenceEqual(preamble) ? prefix.Length : 0;
    }
}
