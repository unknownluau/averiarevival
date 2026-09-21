namespace Averia.RccServiceArbiter.Rcc;

public interface IRccSoapClient
{
    Task OpenJobExAsync(Job job, ScriptExecution script, CancellationToken cancellationToken);
    Task<IReadOnlyList<LuaValue>> BatchJobAsync(Job job, ScriptExecution script, CancellationToken cancellationToken);
    Task<IReadOnlyList<LuaValue>> BatchJobExAsync(Job job, ScriptExecution script, CancellationToken cancellationToken);
    Task ExecuteExAsync(string jobId, ScriptExecution script, CancellationToken cancellationToken);
    Task CloseJobAsync(string jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RccServiceJob>> GetAllJobsAsync(CancellationToken cancellationToken);
    async Task<RccRenderResponse> BatchRenderAsync(Job job, ScriptExecution script, bool modern,
        bool jsonOutput, int maximumBytes, CancellationToken cancellationToken)
    {
        var values = modern
            ? await BatchJobAsync(job, script, cancellationToken)
            : await BatchJobExAsync(job, script, cancellationToken);
        var value = values.FirstOrDefault()?.Value ?? string.Empty;
        var data = jsonOutput ? System.Text.Encoding.UTF8.GetBytes(value) : Convert.FromBase64String(value);
        if (data.Length > maximumBytes) throw new InvalidDataException("RCC render output exceeded the configured limit");
        return new RccRenderResponse(data, values.Skip(1).SelectMany(item => item.Table).Select(item => item.Value)
            .Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToArray());
    }
}

public sealed record RccRenderResponse(byte[] Data, IReadOnlyList<string> DependencyUrls);

public interface IRccSoapClientFactory
{
    IRccSoapClient Create(int port);
}
