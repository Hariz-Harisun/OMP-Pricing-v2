namespace OmpPricing.App.Pipeline;

/// <summary>
/// Writes the finished workbook into the configured output folder under the sender's file name plus
/// a UTC timestamp: name_yyyyMMdd-HHmmss.xlsx. An existing file is never overwritten.
/// </summary>
public sealed class FolderResultSink : IResultSink
{
    private readonly string _folder;
    private readonly TimeProvider _time;

    public FolderResultSink(string folder, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("An output folder is required.", nameof(folder));
        }

        _folder = Path.GetFullPath(folder);
        _time = timeProvider ?? TimeProvider.System;
    }

    public string Folder => _folder;

    public async Task<string> SaveAsync(InputWorkbook workbook, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);

        var target = BuildPath(workbook.FileName);
        await File.WriteAllBytesAsync(target, workbook.Content, cancellationToken);
        return target;
    }

    /// <summary>Builds the timestamped name; adds -1, -2 only when two runs land in the same second.</summary>
    public string BuildPath(string sourceFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(sourceFileName);
        var extension = Path.GetExtension(sourceFileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".xlsx";
        }

        var stamp = _time.GetUtcNow().ToString("yyyyMMdd-HHmmss");
        var candidate = Path.Combine(_folder, stem + "_" + stamp + extension);

        var suffix = 0;
        while (File.Exists(candidate))
        {
            suffix++;
            candidate = Path.Combine(_folder, stem + "_" + stamp + "-" + suffix.ToString() + extension);
        }

        return candidate;
    }
}
