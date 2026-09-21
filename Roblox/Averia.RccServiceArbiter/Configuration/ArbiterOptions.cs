using System.ComponentModel.DataAnnotations;

namespace Averia.RccServiceArbiter.Configuration;

public sealed class ArbiterOptions
{
    [Required]
    public string PublicIp { get; set; } = "127.0.0.1";

    [Required]
    public string BaseUrl { get; set; } = "http://www.averia.lol";

    public string ServiceUrl { get; set; } = string.Empty;

    [Required]
    public string SoapServiceUrl { get; set; } = "projex.zip";

    [Required]
    public string RccServiceRoot { get; set; } = "RCCService";

    [Required]
    public string QuilkinPath { get; set; } = "quilkin.exe";

    public bool ForcedFilteringEnabled { get; set; }

    public string GlobalMessageTopic { get; set; } = "GlobalMessage_KRNE";

    [Range(0, 300)]
    public int PostStartDelaySeconds { get; set; } = 15;

    public string GameServerApiKey { get; set; } = string.Empty;

    public string PlaceVisitAccessKey { get; set; } = string.Empty;

    [Required]
    public ArbiterPortOptions Ports { get; set; } = new();

    [Required]
    public ArbiterProcessOptions Processes { get; set; } = new();

    [Required]
    public ArbiterRenderOptions Render { get; set; } = new();
}

public sealed class ArbiterRenderOptions
{
    [Range(2000, 2100)] public int DefaultYear { get; set; } = 2018;
    [Range(1, 256)] public int MaxWorkers { get; set; } = 8;
    [Range(0, 256)] public int MinimumWarmWorkers { get; set; } = 3;
    [Range(1, 256)] public int MaximumIdleWorkers { get; set; } = 8;
    [Range(1, 10000)] public int QueueCapacity { get; set; } = 128;
    [Range(1, 10000)] public int InteractiveQueueCapacity { get; set; } = 64;
    [Range(1, 10000)] public int BackgroundQueueCapacity { get; set; } = 128;
    [Range(1, 10000)] public int ConversionQueueCapacity { get; set; } = 8;
    [Range(1, 16)] public int ConversionConcurrency { get; set; } = 2;
    [Range(1, 1000)] public int MaxReuseCount { get; set; } = 50;
    [Range(1, 3600)] public int IdleTtlSeconds { get; set; } = 300;
    [Range(1, 300)] public int JobTimeoutSeconds { get; set; } = 60;
    [Range(1, 4096)] public int MaxDimension { get; set; } = 1920;
    [Range(1, 1024)] public int MaxInputMegabytes { get; set; } = 250;
    [Range(1, 1024)] public int MaxOutputMegabytes { get; set; } = 64;
    public string PlaceConverterPath { get; set; } = "RobloxPlaceConverter.exe";
    public string OriginBaseUrl { get; set; } = string.Empty;
    public bool UseBinaryTransport { get; set; } = true;
}

public sealed class ArbiterPortOptions
{
    [Required]
    public PortRange Rcc { get; set; } = new() { Start = 45000, End = 47000 };

    [Required]
    public PortRange GameServer { get; set; } = new() { Start = 50000, End = 60000 };

    [Required]
    public PortRange Proxy { get; set; } = new() { Start = 30000, End = 40000 };

    [Range(0, 3600)]
    public int RecentlyUsedHoldSeconds { get; set; } = 30;
}

public sealed class PortRange
{
    [Range(1, 65535)]
    public int Start { get; set; }

    [Range(1, 65535)]
    public int End { get; set; }
}

public sealed class ArbiterProcessOptions
{
    [Range(1, 10000)]
    public int MaxActiveProcesses { get; set; } = 256;

    [Range(1, 10000)]
    public int MaxActivePerYear { get; set; } = 128;

    [Range(0, 100)]
    public int ReservePerYear { get; set; } = 2;

    [Range(1, 1000)]
    public int MaxReuseCount { get; set; } = 5;

    [Range(1, 86400)]
    public int IdleTtlSeconds { get; set; } = 300;

    [Range(1, 300)]
    public int StartupTimeoutSeconds { get; set; } = 15;

    [Range(1, 300)]
    public int ShutdownTimeoutSeconds { get; set; } = 5;

    [Range(1, int.MaxValue)]
    public int JobExpirationSeconds { get; set; } = 6000000;

    [Range(1, 300)]
    public int CleanupIntervalSeconds { get; set; } = 5;
}
