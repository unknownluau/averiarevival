using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Rcc;
using Microsoft.Extensions.Options;
using Roblox.Rendering;

namespace Averia.RccServiceArbiter.Rendering;

public sealed class RenderScriptCatalog : IRenderScriptCatalog
{
    private readonly ArbiterOptions _options;
    private readonly IReadOnlyDictionary<string, string> _scripts;

    public RenderScriptCatalog(IOptions<ArbiterOptions> options)
    {
        _options = options.Value;
        _scripts = typeof(RenderScriptCatalog).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Averia.RccServiceArbiter.RenderScripts.", StringComparison.Ordinal))
            .ToDictionary(name => name, ReadResource, StringComparer.OrdinalIgnoreCase);
    }

    public ScriptExecution Create(RenderRequest request)
    {
        if (_options.Render.DefaultYear >= 2018)
        {
            return new ScriptExecution
            {
                Name = request.Kind.ToString(),
                Script = CreateModernJson(request),
                Arguments = Array.Empty<LuaValue>(),
            };
        }

        var scriptName = ScriptName(request.Kind);
        var script = GetScript(scriptName);
        return new ScriptExecution
        {
            Name = request.Kind.ToString(),
            Script = script,
            Arguments = BuildArguments(request),
        };
    }

    private string CreateModernJson(RenderRequest request)
    {
        var baseUrl = OriginBaseUrl();
        var assetUrl = request.AssetUrl == null ? AssetUrl(request.AssetId, request) : PrivateDependencyUrl(request.AssetUrl, request);
        var appearanceUrl = request.CharacterAppearanceUrl == null
            ? Correlate($"{baseUrl}v1.1/avatar-fetch?placeId=0&userId={request.UserId}", request)
            : PrivateDependencyUrl(request.CharacterAppearanceUrl, request);
        var format = request.Kind == RenderKind.Avatar3D ? "OBJ" : "PNG";
        var type = request.Kind switch
        {
            RenderKind.Avatar when request.AvatarRigType == AvatarRigType.R6 => "Avatar",
            RenderKind.Avatar or RenderKind.Avatar3D => "Avatar_R15_Action",
            RenderKind.AvatarHeadshot => "Closeup",
            RenderKind.Asset or RenderKind.Model => "Model",
            RenderKind.Texture => request.IsFace ? "Face" : "Image",
            // Modern RCC installations do not register a distinct TeeShirt thumbnail
            // operation. A T-shirt points at an Image asset, so resolve that image with
            // the built-in Image operation and let the caller apply presentation styling.
            RenderKind.TeeShirt => "Image",
            RenderKind.Hat => "Hat",
            RenderKind.Head => "Head",
            RenderKind.Mesh => "Mesh",
            RenderKind.MeshPart => "MeshPart",
            RenderKind.Package => "Package",
            RenderKind.BodyPart => "BodyPart",
            RenderKind.Clothing => "Shirt",
            RenderKind.Place => "Place",
            RenderKind.Animation => "AvatarAnimation",
            RenderKind.AnimationSilhouette => "AnimationSilhouette",
            _ => throw new RenderValidationException($"Render kind {request.Kind} is not an RCC JSON thumbnail operation"),
        };
        object?[] arguments = request.Kind switch
        {
            RenderKind.Avatar when request.AvatarRigType == AvatarRigType.R6 => [appearanceUrl, baseUrl, "PNG", request.Width, request.Height],
            RenderKind.Avatar or RenderKind.Avatar3D => [baseUrl, appearanceUrl, format, request.Kind == RenderKind.Avatar3D ? 352 : request.Width, request.Kind == RenderKind.Avatar3D ? 352 : request.Height, true, 30, 100, 0, 0],
            RenderKind.AvatarHeadshot => [baseUrl, appearanceUrl, "PNG", request.Width, request.Height, true, 40, 60, 0, 0],
            RenderKind.Texture => [request.AssetId ?? 0, baseUrl, "PNG", request.Width, request.Height, true, 0, 0, 0, 0],
            RenderKind.TeeShirt => [request.ContentId ?? request.AssetId ?? 0, baseUrl, "PNG", request.Width, request.Height, true, 0, 0, 0, 0],
            RenderKind.Head => [assetUrl, "PNG", request.Width, request.Height, baseUrl, 420, true, 0, 0, 0, 0],
            RenderKind.Package => [PrivateDependencyUrls(request.AssetUrls, request), baseUrl, "PNG", request.Width, request.Height, AssetUrl(1785197, request), string.Empty, true, 0, 0, 0, 0],
            RenderKind.BodyPart => [assetUrl, baseUrl, "PNG", request.Width, request.Height, AssetUrl(1785197, request), string.Empty],
            RenderKind.Clothing => [assetUrl, "PNG", request.Width, request.Height, baseUrl, 1785197, true, 0, 0, 0, 0],
            RenderKind.Place => [assetUrl, "PNG", request.Width, request.Height, baseUrl, request.AssetId ?? 0, baseUrl, 1],
            RenderKind.Animation => [appearanceUrl, baseUrl, "PNG", request.Width, request.Height, PrivateDependencyUrl(request.AnimationUrl, request)],
            RenderKind.AnimationSilhouette => [assetUrl, baseUrl, request.Width, request.Height],
            _ => [assetUrl, "PNG", request.Width, request.Height, baseUrl, true, 0, 0, 0, 0],
        };
        var template = JsonNode.Parse(GetScript(ModernTemplateName(request.Kind)))?.AsObject()
            ?? throw new RenderExecutionException($"Modern render template for {request.Kind} is invalid");
        var settings = template["Settings"]?.AsObject()
            ?? throw new RenderExecutionException($"Modern render template for {request.Kind} has no Settings object");
        settings["Type"] = type;
        settings["Arguments"] = JsonSerializer.SerializeToNode(arguments);
        return template.ToJsonString();
    }

    private static string ModernTemplateName(RenderKind kind) => kind switch
    {
        RenderKind.Avatar or RenderKind.Avatar3D => "Avatar.json",
        RenderKind.AvatarHeadshot => "Closeup.json",
        RenderKind.Asset or RenderKind.Model => "Model.json",
        RenderKind.Texture => "Image.json",
        RenderKind.TeeShirt => "Image.json",
        RenderKind.Hat => "Hat.json",
        RenderKind.Head => "Head.json",
        RenderKind.Mesh => "Mesh.json",
        RenderKind.MeshPart => "MeshPart.json",
        RenderKind.Package => "Package.json",
        RenderKind.BodyPart => "BodyPart.json",
        RenderKind.Clothing => "Clothing.json",
        RenderKind.Place => "Place.json",
        RenderKind.Animation => "AvatarAnimation.json",
        RenderKind.AnimationSilhouette => "AnimationSilhouette.json",
        _ => throw new RenderValidationException($"Render kind {kind} has no modern JSON template"),
    };

    private IReadOnlyList<LuaValue> BuildArguments(RenderRequest request)
    {
        var assetUrl = request.AssetUrl == null ? AssetUrl(request.AssetId, request) : PrivateDependencyUrl(request.AssetUrl, request);
        var appearance = request.CharacterAppearanceUrl ??
                         Correlate($"{OriginBaseUrl()}v1.1/avatar-fetch?userId={request.UserId}", request);
        appearance = PrivateDependencyUrl(appearance, request);
        var common = new[] { String(assetUrl), String("PNG"), Number(request.Width), Number(request.Height), String(OriginBaseUrl()) };
        return request.Kind switch
        {
            RenderKind.Avatar => [String(appearance), String(OriginBaseUrl()), String("PNG"), Number(request.Width), Number(request.Height)],
            RenderKind.AvatarHeadshot => [String(OriginBaseUrl()), String(appearance), String("PNG"), Number(request.Width), Number(request.Height), Bool(true), Number(30), Number(100), Number(0), Number(0)],
            RenderKind.Head or RenderKind.Clothing => [.. common, Number(420)],
            RenderKind.Place => [.. common, Number(request.AssetId ?? 0)],
            RenderKind.Package => [String(PrivateDependencyUrls(request.AssetUrls, request)), String(OriginBaseUrl()), String("PNG"), Number(request.Width), Number(request.Height), String(AssetUrl(1785197, request)), String(string.Empty)],
            RenderKind.Animation => [String(PrivateDependencyUrl(request.CharacterAppearanceUrl, request)), String(PrivateDependencyUrl(request.AnimationUrl, request)), String("PNG"), Number(request.Width), Number(request.Height), String(OriginBaseUrl())],
            _ => common,
        };
    }

    private static string ScriptName(RenderKind kind) => kind switch
    {
        RenderKind.Avatar or RenderKind.Avatar3D => "AvatarScript.lua",
        RenderKind.AvatarHeadshot => "AvatarHeadShotScript.lua",
        RenderKind.Texture => "FaceScript.lua",
        RenderKind.Hat => "HatScript.lua",
        RenderKind.Head => "HeadScript.lua",
        RenderKind.Mesh => "MeshScript.lua",
        RenderKind.MeshPart => "MeshPartScript.lua",
        RenderKind.Model or RenderKind.Asset or RenderKind.Animation or RenderKind.AnimationSilhouette => "ModelScript.lua",
        RenderKind.Package or RenderKind.BodyPart => "PackageScript.lua",
        RenderKind.Clothing => "ShirtScript.lua",
        RenderKind.Place => "PlaceScript.lua",
        RenderKind.TeeShirt => "FaceScript.lua",
        _ => throw new RenderValidationException($"Render kind {kind} is not an RCC thumbnail operation"),
    };

    private string AssetUrl(long? assetId, RenderRequest request) => Correlate($"{OriginBaseUrl()}asset/?id={assetId ?? 0}", request);
    private string PrivateDependencyUrls(string? urls, RenderRequest request) => string.Join(";",
        (urls ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries).Select(url => PrivateDependencyUrl(url, request)));
    private string PrivateDependencyUrl(string? url, RenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return Correlate(url, request);
        var origin = new Uri(OriginBaseUrl());
        var rewritten = new UriBuilder(parsed) { Scheme = origin.Scheme, Host = origin.Host, Port = origin.IsDefaultPort ? -1 : origin.Port };
        return Correlate(rewritten.Uri.ToString(), request);
    }
    private static string Correlate(string url, RenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CorrelationId)) return url;
        return url + (url.Contains('?') ? "&" : "?") + "renderCorrelationId=" + Uri.EscapeDataString(request.CorrelationId);
    }
    private string OriginBaseUrl() => (string.IsNullOrWhiteSpace(_options.Render.OriginBaseUrl)
        ? _options.BaseUrl
        : _options.Render.OriginBaseUrl).TrimEnd('/') + "/";
    private string GetScript(string fileName) => _scripts.FirstOrDefault(pair => pair.Key.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)).Value
        ?? throw new RenderExecutionException($"Embedded render script {fileName} was not found");
    private static LuaValue String(string value) => new() { Type = LuaType.LUA_TSTRING, Value = value };
    private static LuaValue Number(long value) => new() { Type = LuaType.LUA_TNUMBER, Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    private static LuaValue Bool(bool value) => new() { Type = LuaType.LUA_TBOOLEAN, Value = value ? "true" : "false" };
    private static string ReadResource(string name)
    {
        using var stream = typeof(RenderScriptCatalog).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
