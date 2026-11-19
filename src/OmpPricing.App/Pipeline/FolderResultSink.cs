namespace OmpPricing.App.Pipeline;

/// <summary>Writes the finished workbook into the configured output folder under the sender's file name.</summary>
public sealed class FolderResultSink : IResultSink
{
    private readonly string _folder;

    public FolderResultSink(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("An output folder is required.", nameof(folder));
        }

        _folder = Path.GetFullPath(folder);
    }

    public string Folder => _folder;

    public async Task<string> SaveAsync(InputWorkbook workbook, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);

        var target = Path.Combine(_folder, workbook.FileName);
        await File.WriteAllBytesAsync(target, workbook.Content, cancellationToken);
        return target;
    }
}
