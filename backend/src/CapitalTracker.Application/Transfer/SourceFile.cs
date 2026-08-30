namespace CapitalTracker.Application.Transfer;

/// <summary>
/// Whatever the owner uploaded, as a grid. The two shapes it arrives in — a spreadsheet
/// from a bank and a CSV from us — differ only here; everything past this point works on
/// rows of strings.
/// </summary>
public static class SourceFile
{
    public static bool TryReadGrid(byte[] content, out List<string[]> grid, out string? problem)
    {
        grid = [];
        problem = null;

        if (XlsxReader.LooksLikeXlsx(content))
        {
            try
            {
                grid = XlsxReader.Read(content);
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
            {
                problem = "Не вдалося прочитати книгу Excel — можливо, файл пошкоджений.";
                return false;
            }
        }

        if (!ImportDtoMapping.TryDecode(content, out var text))
        {
            problem = "Файл не у кодуванні UTF-8. Збережіть його як CSV UTF-8 або завантажте .xlsx.";
            return false;
        }

        var stripped = CsvReader.StripBom(text);
        grid = CsvReader.Read(stripped, CsvReader.DetectDelimiter(stripped));
        return true;
    }

    /// <summary>
    /// True when the file already speaks our language — our own export coming back. It goes
    /// straight to the diff, with no column mapping to fill in.
    /// </summary>
    public static bool LooksCanonical(List<string[]> grid)
    {
        if (grid.Count == 0)
            return false;

        var header = grid[0].Select(c => c.Trim().ToLowerInvariant()).ToHashSet();
        return header.Contains("подія") && header.Contains("дата");
    }
}
