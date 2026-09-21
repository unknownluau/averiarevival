using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using System.Xml;

namespace Averia.RccServiceArbiter.Rcc;

public sealed class RccSoapClient : IRccSoapClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _serviceUrl;

    public RccSoapClient(HttpClient httpClient, Uri endpoint, string serviceUrl)
    {
        _httpClient = httpClient;
        _endpoint = endpoint;
        _serviceUrl = serviceUrl;
    }

    public Task OpenJobExAsync(Job job, ScriptExecution script, CancellationToken cancellationToken)
    {
        return SendAsync("OpenJobEx", RccSoapEnvelope.OpenJobEx(_serviceUrl, job, script), cancellationToken);
    }

    public async Task<IReadOnlyList<LuaValue>> BatchJobExAsync(Job job, ScriptExecution script, CancellationToken cancellationToken)
    {
        var response = await SendForResponseAsync("BatchJobEx", RccSoapEnvelope.BatchJobEx(_serviceUrl, job, script), cancellationToken);
        return ParseBatchJobResponse(response);
    }

    public async Task<IReadOnlyList<LuaValue>> BatchJobAsync(Job job, ScriptExecution script, CancellationToken cancellationToken)
    {
        var response = await SendForResponseAsync("BatchJob", RccSoapEnvelope.BatchJob(_serviceUrl, job, script), cancellationToken);
        return ParseBatchJobResponse(response);
    }

    public async Task<RccRenderResponse> BatchRenderAsync(Job job, ScriptExecution script, bool modern,
        bool jsonOutput, int maximumBytes, CancellationToken cancellationToken)
    {
        var action = modern ? "BatchJob" : "BatchJobEx";
        var envelope = modern ? RccSoapEnvelope.BatchJob(_serviceUrl, job, script) : RccSoapEnvelope.BatchJobEx(_serviceUrl, job, script);
        using var request = CreateRequest(action, envelope);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"RCC {action} returned HTTP {(int)response.StatusCode}: {error}", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        });
        byte[]? data = null;
        List<string> dependencies = [];
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "value") continue;
            if (data == null)
            {
                if (jsonOutput)
                {
                    var text = await reader.ReadElementContentAsStringAsync();
                    data = Encoding.UTF8.GetBytes(text);
                }
                else
                {
                    using var output = new MemoryStream();
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await reader.ReadElementContentAsBase64Async(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > maximumBytes)
                            throw new InvalidDataException("RCC render output exceeded the configured limit");
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                    data = output.ToArray();
                }
            }
            else
            {
                var dependency = await reader.ReadElementContentAsStringAsync();
                if (Uri.TryCreate(dependency, UriKind.Absolute, out _)) dependencies.Add(dependency);
            }
        }
        return new RccRenderResponse(data ?? Array.Empty<byte>(), dependencies.Distinct().ToArray());
    }

    public static IReadOnlyList<LuaValue> ParseBatchJobResponse(string response)
    {
        var document = XDocument.Parse(response);
        var results = document.Descendants().Where(element =>
            element.Name.LocalName is "BatchJobExResult" or "BatchJobResult").ToList();
        if (results.Count == 0)
        {
            return Array.Empty<LuaValue>();
        }
        var values = new List<LuaValue>();
        foreach (var result in results)
        {
            if (IsLuaValue(result)) { values.Add(ParseLuaValue(result)); continue; }
            values.AddRange(result.Descendants().Where(IsLuaValue)
                .Where(element => !element.Ancestors().TakeWhile(ancestor => ancestor != result).Any(IsLuaValue))
                .Select(ParseLuaValue));
        }
        return values;
    }

    public Task ExecuteExAsync(string jobId, ScriptExecution script, CancellationToken cancellationToken)
    {
        return SendAsync("ExecuteEx", RccSoapEnvelope.ExecuteEx(_serviceUrl, jobId, script), cancellationToken);
    }

    public Task CloseJobAsync(string jobId, CancellationToken cancellationToken)
    {
        return SendAsync("CloseJob", RccSoapEnvelope.CloseJob(_serviceUrl, jobId), cancellationToken);
    }

    public async Task<IReadOnlyList<RccServiceJob>> GetAllJobsAsync(CancellationToken cancellationToken)
    {
        var response = await SendForResponseAsync("GetAllJobs", RccSoapEnvelope.GetAllJobs(_serviceUrl), cancellationToken);
        if (string.IsNullOrWhiteSpace(response))
        {
            return Array.Empty<RccServiceJob>();
        }

        var document = XDocument.Parse(response);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "Job")
            .Select(element => new RccServiceJob
            {
                Id = element.Elements().FirstOrDefault(child => child.Name.LocalName == "id")?.Value ?? string.Empty,
            })
            .Where(job => !string.IsNullOrWhiteSpace(job.Id))
            .ToList();
    }

    private async Task SendAsync(string action, XDocument document, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(action, document);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> SendForResponseAsync(string action, XDocument document, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(action, document);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"RCC {action} returned HTTP {(int)response.StatusCode}: {body}", null, response.StatusCode);
        }
        return body;
    }

    private HttpRequestMessage CreateRequest(string action, XDocument document)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.TryAddWithoutValidation("SOAPAction", RccSoapEnvelope.SoapAction(_serviceUrl, action));
        request.Content = new StringContent(RccSoapEnvelope.ToRequestBody(document), Encoding.UTF8);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8");
        return request;
    }

    private static bool IsLuaValue(XElement element) =>
        element.Elements().Any(child => child.Name.LocalName == "type");

    private static LuaValue ParseLuaValue(XElement element)
    {
        var typeText = element.Elements().FirstOrDefault(child => child.Name.LocalName == "type")?.Value;
        var table = element.Elements().FirstOrDefault(child => child.Name.LocalName == "table");
        return new LuaValue
        {
            Type = Enum.TryParse<LuaType>(typeText, true, out var type) ? type : LuaType.LUA_TNIL,
            Value = element.Elements().FirstOrDefault(child => child.Name.LocalName == "value")?.Value ?? string.Empty,
            Table = table == null
                ? Array.Empty<LuaValue>()
                : table.Elements().Where(IsLuaValue).Select(ParseLuaValue).ToList(),
        };
    }
}
