using System.Text.Json;

namespace Averia.RccServiceArbiter.Rcc;

public static class RccScriptFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    public static ScriptExecution GameServer(string payload)
    {
        return new ScriptExecution
        {
            Name = "ZUNA_GAME",
            Script = payload,
        };
    }

    public static ScriptExecution EvictPlayer(long userId, int messageVersionId)
    {
        return new ScriptExecution
        {
            Name = "Evict Player V1",
            Script = CreateRequest("EvictPlayer", new Dictionary<string, object?>
            {
                ["MessageVersion"] = messageVersionId,
                ["PlayerId"] = userId,
            }),
        };
    }

    public static ScriptExecution SetFilteringEnabled(bool isEnabled)
    {
        return new ScriptExecution
        {
            Name = "ToggleFilteringEnabled v1",
            Script = CreateRequest("Thumbnail", new Dictionary<string, object?>
            {
                ["Type"] = "ToggleFilteringEnabled",
                ["Arguments"] = new object[] { isEnabled },
            }),
        };
    }

    public static ScriptExecution GlobalMessage(string topic)
    {
        return new ScriptExecution
        {
            Name = "GlobalMessageModule v1",
            Script = CreateRequest("Thumbnail", new Dictionary<string, object?>
            {
                ["Type"] = "GlobalMessage",
                ["Arguments"] = new object[] { topic },
            }),
        };
    }

    public static ScriptExecution Shutdown()
    {
        return new ScriptExecution
        {
            Name = "Close Server V1",
            Script = CreateRequest("ServerAction", new Dictionary<string, object?>
            {
                ["Action"] = "Shutdown",
                ["Reason"] = "Developer",
                ["VerboseReason"] = "meow",
            }),
        };
    }


    private static string CreateRequest(string mode, object payload)
    {
        return JsonSerializer.Serialize(new
        {
            Mode = mode,
            Settings = payload,
        }, JsonOptions);
    }
}
