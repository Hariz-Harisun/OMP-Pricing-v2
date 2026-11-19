namespace OmpPricing.App.Pipeline;

/// <summary>
/// Where the finished workbook goes. Today only <see cref="FolderResultSink"/> exists; sending the
/// file by email plugs in later as another implementation.
/// </summary>
public interface IResultSink
{
    /// <summary>Stores the finished workbook and returns where it was written.</summary>
    Task<string> SaveAsync(InputWorkbook workbook, CancellationToken cancellationToken);
}
