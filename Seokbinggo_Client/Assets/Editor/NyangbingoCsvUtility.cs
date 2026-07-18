using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class NyangbingoCsvUtility
{
    public static List<Dictionary<string, string>> ReadRows(string path)
        => ReadRows(path, false);

    public static List<Dictionary<string, string>> ReadRows(string path, bool mergeUnquotedTrailingNote)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var rows = new List<Dictionary<string, string>>();
        if (lines.Length == 0) throw new InvalidDataException($"CSV '{path}' has no header row.");
        var headers = Split(lines[0], path, 1);
        var uniqueHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
            if (string.IsNullOrWhiteSpace(headers[i]) || !uniqueHeaders.Add(headers[i]))
                throw new InvalidDataException($"CSV '{path}' has a blank or duplicate header at column {i + 1}.");

        var hasId = uniqueHeaders.Contains("id");
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        for (var rowIndex = 1; rowIndex < lines.Length; rowIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[rowIndex])) continue;
            var values = Split(lines[rowIndex], path, rowIndex + 1);
            if (mergeUnquotedTrailingNote && values.Count > headers.Count &&
                string.Equals(headers[headers.Count - 1], "note", StringComparison.OrdinalIgnoreCase))
            {
                var mergedNote = new StringBuilder(values[headers.Count - 1]);
                for (var valueIndex = headers.Count; valueIndex < values.Count; valueIndex++)
                    mergedNote.Append(',').Append(values[valueIndex]);
                values.RemoveRange(headers.Count - 1, values.Count - headers.Count + 1);
                values.Add(mergedNote.ToString());
            }
            if (values.Count != headers.Count)
                throw new InvalidDataException(
                    $"CSV '{path}' row {rowIndex + 1} has {values.Count} columns; expected {headers.Count}.");
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++) row.Add(headers[i], values[i]);
            if (hasId && (!row.TryGetValue("id", out var id) || string.IsNullOrWhiteSpace(id) || !uniqueIds.Add(id)))
                throw new InvalidDataException($"CSV '{path}' row {rowIndex + 1} has a blank or duplicate ID.");
            rows.Add(row);
        }
        return rows;
    }

    private static List<string> Split(string line, string path, int lineNumber)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;
        var quoteClosed = false;
        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (quoted)
            {
                if (character != '"')
                {
                    builder.Append(character);
                    continue;
                }
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                    continue;
                }
                quoted = false;
                quoteClosed = true;
                continue;
            }

            if (quoteClosed)
            {
                if (character == ',')
                {
                    values.Add(builder.ToString().Trim());
                    builder.Clear();
                    quoteClosed = false;
                }
                else if (!char.IsWhiteSpace(character))
                    throw new InvalidDataException($"CSV '{path}' row {lineNumber} has text after a closing quote.");
                continue;
            }

            if (character == ',')
            {
                values.Add(builder.ToString().Trim());
                builder.Clear();
            }
            else if (character == '"')
            {
                if (builder.ToString().Trim().Length > 0)
                    throw new InvalidDataException($"CSV '{path}' row {lineNumber} has a quote inside an unquoted field.");
                builder.Clear();
                quoted = true;
            }
            else builder.Append(character);
        }

        if (quoted) throw new InvalidDataException($"CSV '{path}' row {lineNumber} has an unclosed quote.");
        values.Add(builder.ToString().Trim());
        return values;
    }
}
