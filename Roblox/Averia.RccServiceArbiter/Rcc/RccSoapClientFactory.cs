namespace Averia.RccServiceArbiter.Rcc;

using Averia.RccServiceArbiter.Configuration;
using Microsoft.Extensions.Options;

public sealed class RccSoapClientFactory : IRccSoapClientFactory
{
    private readonly HttpClient _httpClient;
    private readonly ArbiterOptions _options;

    public RccSoapClientFactory(HttpClient httpClient, IOptions<ArbiterOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public IRccSoapClient Create(int port)
    {
        return new RccSoapClient(_httpClient, new Uri($"http://127.0.0.1:{port}/"), _options.SoapServiceUrl);
    }
}
