namespace OmpPricing.App.Pipeline;

/// <summary>Reads a workbook from a path given with --file.</summary>
public sealed class FileInputSource : IInputSource
{
    private readonly string _path;

    public FileInputSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
    }

    public string FilePath => _path;

    public async Task<InputWorkbook> OpenAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException("Input workbook not found: " + _path, _path);
        }

        var content = await File.ReadAllBytesAsync(_path, cancellationToken);
        return new InputWorkbook(Path.GetFileName(_path), content);
    }
}
