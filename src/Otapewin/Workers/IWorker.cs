namespace Otapewin.Workers;

public interface IWorker
{
    Task ProcessAsync(CancellationToken token);
}
