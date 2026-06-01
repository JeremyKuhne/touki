// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki.Io;

/// <summary>
///  Minimal helpers for reading and writing a single CSV field with RFC 4180 style quoting.
/// </summary>
public static class CsvField
{
    /// <summary>
    ///  Writes <paramref name="value"/> as a CSV field, quoting it only when required.
    /// </summary>
    public static void Write(TextWriter writer, string value)
    {
        if (NeedsQuoting(value))
        {
            writer.Write('"');
            foreach (char c in value)
            {
                if (c == '"')
                {
                    writer.Write('"');
                }

                writer.Write(c);
            }

            writer.Write('"');
        }
        else
        {
            writer.Write(value);
        }
    }

    /// <summary>
    ///  Parses a CSV line into exactly its first two fields (type and value). Additional fields,
    ///  if any, are ignored.
    /// </summary>
    /// <returns><see langword="true"/> when at least one field was parsed.</returns>
    public static bool TryParse(string line, out char type, out string value)
    {
        type = '\0';
        value = string.Empty;

        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        int position = 0;
        string first = ReadField(line, ref position);
        if (first.Length == 0)
        {
            return false;
        }

        type = first[0];

        if (position < line.Length)
        {
            value = ReadField(line, ref position);
        }

        return true;
    }

    /// <summary>
    ///  Writes <paramref name="fields"/> as a single comma-separated CSV record, quoting each field
    ///  only when required, and terminates the line with a single newline.
    /// </summary>
    public static void WriteRecord(TextWriter writer, params string[] fields)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            Write(writer, fields[i]);
        }

        writer.Write('\n');
    }

    /// <summary>
    ///  Parses a CSV line into all of its fields, unescaping quoted fields.
    /// </summary>
    public static List<string> ParseRecord(string line)
    {
        List<string> fields = [];
        if (string.IsNullOrEmpty(line))
        {
            return fields;
        }

        int position = 0;
        while (true)
        {
            fields.Add(ReadField(line, ref position));
            if (position >= line.Length)
            {
                break;
            }
        }

        return fields;
    }

    private static bool NeedsQuoting(string value)
    {
        foreach (char c in value)
        {
            if (c is ',' or '"' or '\r' or '\n')
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadField(string line, ref int position)
    {
        if (position >= line.Length)
        {
            return string.Empty;
        }

        if (line[position] == '"')
        {
            // Quoted field: read until the closing quote, unescaping doubled quotes.
            position++;
            StringBuilder builder = new();
            while (position < line.Length)
            {
                char c = line[position++];
                if (c == '"')
                {
                    if (position < line.Length && line[position] == '"')
                    {
                        builder.Append('"');
                        position++;
                        continue;
                    }

                    break;
                }

                builder.Append(c);
            }

            // Skip the trailing comma if present.
            if (position < line.Length && line[position] == ',')
            {
                position++;
            }

            return builder.ToString();
        }

        // Unquoted field: read until the next comma.
        int start = position;
        while (position < line.Length && line[position] != ',')
        {
            position++;
        }

        string result = line[start..position];

        if (position < line.Length && line[position] == ',')
        {
            position++;
        }

        return result;
    }
}
