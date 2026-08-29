using System.Text;

namespace CapitalTracker.Application.Transfer;

/// <summary>
/// The CSV grammar itself, and nothing above it: quoted fields, quotes escaped by doubling,
/// delimiters and line breaks living inside quotes. Hand-rolled rather than pulled in as a
/// dependency because this is the whole of it — but hand-rolled carefully, since a naive
/// Split(';') is exactly how a note containing a semicolon silently shifts every column
/// after it by one.
/// </summary>
public static class CsvReader
{
    /// <summary>
    /// Decided from the header line rather than configured: exports written here use
    /// semicolons, half the world writes commas, and the file itself already says which.
    /// Counting outside quotes matters — a comma inside "Позняки, 1" is not a separator.
    /// </summary>
    public static char DetectDelimiter(string text)
    {
        var line = FirstLine(text);
        return CountOutsideQuotes(line, ';') >= CountOutsideQuotes(line, ',') ? ';' : ',';
    }

    public static List<string[]> Read(string text, char delimiter)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var sawAnything = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // A doubled quote is a literal one; a single quote closes the field.
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    sawAnything = true;
                    break;

                case '\r':
                    // Swallowed: the line ends on the \n that follows it.
                    break;

                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    if (sawAnything || fields.Count > 1)
                        rows.Add([.. fields]);
                    fields.Clear();
                    sawAnything = false;
                    break;

                default:
                    if (c == delimiter)
                    {
                        fields.Add(field.ToString());
                        field.Clear();
                    }
                    else
                    {
                        field.Append(c);
                        sawAnything = true;
                    }

                    break;
            }
        }

        // Whatever is left when the file doesn't end in a newline.
        if (sawAnything || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add([.. fields]);
        }

        return rows;
    }

    /// <summary>
    /// Strips a UTF-8 BOM. Our own exports carry one so Excel reads the Cyrillic headers,
    /// and left in place it would glue itself to the first column's name.
    /// </summary>
    public static string StripBom(string text) =>
        text.Length > 0 && text[0] == '﻿' ? text[1..] : text;

    private static string FirstLine(string text)
    {
        var end = text.IndexOf('\n');
        return end < 0 ? text : text[..end];
    }

    private static int CountOutsideQuotes(string line, char c)
    {
        var count = 0;
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
                inQuotes = !inQuotes;
            else if (ch == c && !inQuotes)
                count++;
        }

        return count;
    }
}
