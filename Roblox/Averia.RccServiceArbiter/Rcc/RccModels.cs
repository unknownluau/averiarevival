namespace Averia.RccServiceArbiter.Rcc;

public sealed class Job
{
    public string Id { get; set; } = string.Empty;
    public double ExpirationInSeconds { get; set; }
    public int Category { get; set; } = 1;
    public double Cores { get; set; } = 2;
}

public sealed class ScriptExecution
{
    public string Name { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public IReadOnlyList<LuaValue> Arguments { get; set; } = Array.Empty<LuaValue>();
}

public sealed class ServerAction
{
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string VerboseReason { get; set; } = string.Empty;
}

public sealed class LuaValue
{
    public LuaType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public IReadOnlyList<LuaValue> Table { get; set; } = Array.Empty<LuaValue>();
}

public enum LuaType
{
    LUA_TNIL,
    LUA_TBOOLEAN,
    LUA_TNUMBER,
    LUA_TSTRING,
    LUA_TTABLE
}

public sealed class RccServiceJob
{
    public string Id { get; set; } = string.Empty;
}
