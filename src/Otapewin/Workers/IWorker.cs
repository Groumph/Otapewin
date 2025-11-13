namespace Otapewin.Workers;

/// <summary>
/// Interface for worker processes
/// </summary>
public interface IWorker
{
    /// <summary>
    /// Process the worker task asynchronously
    /// </summary>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ProcessAsync(CancellationToken token);
}
