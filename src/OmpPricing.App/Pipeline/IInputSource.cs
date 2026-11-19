namespace OmpPricing.App.Pipeline;

/// <summary>A workbook handed to the pipeline, with the file name it should keep.</summary>
public sealed record InputWorkbook(string FileName, byte[] Content);

/// <summary>
/// Where the pipeline gets its workbook. Today only <see cref="FileInputSource"/> exists; email or a
/// watched folder plug in later without touching the pipeline.
/// </summary>
public interface IInputSource
{
    Task<InputWorkbook> OpenAsync(CancellationToken cancellationToken);
}
