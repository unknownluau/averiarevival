using NetVips;

namespace Roblox.Rendering;

public static class RenderingHandler
{
    public static System.Collections.Concurrent.ConcurrentDictionary<long, string> allowedPlaceForRender { get; } = new();

    public static void Configure(string baseUrl, string authorization = "", bool useBinaryTransport = true) => RenderHttpClient.Configure(baseUrl, authorization, useBinaryTransport);

    private static async Task<string> SendAsync(RenderRequest request, CancellationToken? cancellationToken = null)
    {
        var result = await RenderHttpClient.SendBytesAsync(request, cancellationToken ?? CancellationToken.None);
        return result.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            ? System.Text.Encoding.UTF8.GetString(result.Data)
            : Convert.ToBase64String(result.Data);
    }

    private static RenderRequest Background(RenderKind kind, long assetId, string? workKey = null) => new()
    { Kind = kind, AssetId = assetId, Priority = RenderPriority.Background, WorkKey = workKey ?? $"asset:{assetId}" };

    public static Task<string> RequestHatThumbnail(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.Hat, assetId, workKey), cancellationToken);
    public static Task<string> RequestMeshThumbnail(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.Mesh, assetId, workKey), cancellationToken);
    public static Task<string> RequestMeshPartThumbnail(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.MeshPart, assetId, workKey), cancellationToken);
    public static Task<string> RequestModelThumbnail(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.Model, assetId, workKey), cancellationToken);
    public static Task<string> RequestImageThumbnail(long assetId, bool isFace = false, string? workKey = null, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.Texture, AssetId = assetId, IsFace = isFace,
            Priority = RenderPriority.Background, WorkKey = workKey ?? $"asset:{assetId}" }, cancellationToken);
    public static Task<string> RequestClothingRender(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.Clothing, assetId, workKey), cancellationToken);
    public static Task<string> RequestTeeShirtRender(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.TeeShirt, assetId, workKey), cancellationToken);
    public static Task<string> RequestHeadRender(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.Head, assetId, workKey), cancellationToken);
    public static Task<string> RequestAnimationSilhouetteRender(long assetId, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(Background(RenderKind.AnimationSilhouette, assetId, workKey), cancellationToken);
    public static Task<string> RequestAnimationRender(string characterAppearanceUrl, string animationUrl, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(new RenderRequest
    { Kind = RenderKind.Animation, CharacterAppearanceUrl = characterAppearanceUrl, AnimationUrl = animationUrl, Priority = RenderPriority.Background, WorkKey = workKey }, cancellationToken);
    public static Task<string> RequestPackageRender(string assetUrls, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(new RenderRequest { Kind = RenderKind.Package, AssetUrls = assetUrls, Priority = RenderPriority.Background, WorkKey = workKey }, cancellationToken);
    public static Task<string> RequestBodyPartRender(string assetUrl, string? workKey = null, CancellationToken? cancellationToken = null) => SendAsync(new RenderRequest { Kind = RenderKind.BodyPart, AssetUrl = assetUrl, Priority = RenderPriority.Background, WorkKey = workKey }, cancellationToken);
    public static Task<string> RequestPlayerThumbnail(long userId, AvatarRigType avatarRigType, CancellationToken? cancellationToken = null, string? workKey = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.Avatar, UserId = userId, AvatarRigType = avatarRigType, Width = 704, Height = 704, WorkKey = workKey }, cancellationToken);

    public static Task<string> RequestPlayerThumbnail(long userId, CancellationToken? cancellationToken = null) =>
        RequestPlayerThumbnail(userId, AvatarRigType.R15, cancellationToken);
    public static Task<string> RequestPlayerThumbnail3D(long userId, CancellationToken? cancellationToken = null, string? workKey = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.Avatar3D, UserId = userId, Width = 352, Height = 352, WorkKey = workKey }, cancellationToken);
    public static Task<string> RequestHeadshotThumbnail(long userId, CancellationToken? cancellationToken = null, string? workKey = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.AvatarHeadshot, UserId = userId, Width = 300, Height = 300, WorkKey = workKey }, cancellationToken);

    public static async Task<string> RequestPlaceRender(long assetId, int x, int y, string? workKey = null, CancellationToken? cancellationToken = null)
    {
        allowedPlaceForRender.TryAdd(assetId, string.Empty);
        return await SendAsync(new RenderRequest { Kind = RenderKind.Place, AssetId = assetId, Width = x, Height = y,
            Priority = RenderPriority.Background, WorkKey = workKey ?? $"asset:{assetId}" }, cancellationToken);
    }

    public static Task<TReturn> ResizeImage<TReturn, TImageType>(TImageType inputImage, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive");
        byte[] source = inputImage switch
        {
            string value => Convert.FromBase64String(value),
            byte[] value => value,
            Stream stream when stream.CanRead => ReadStream(stream),
            _ => throw new ArgumentException("Unsupported image input type", nameof(inputImage)),
        };
        using var image = Image.ThumbnailBuffer(source, width, height: height, size: Enums.Size.Force);
        var output = image.PngsaveBuffer();
        object result = typeof(TReturn) == typeof(string) ? Convert.ToBase64String(output)
            : typeof(TReturn) == typeof(byte[]) ? output
            : typeof(TReturn) == typeof(MemoryStream) || typeof(TReturn) == typeof(Stream) ? new MemoryStream(output, writable: false)
            : throw new ArgumentException("Unsupported image return type", nameof(TReturn));
        return Task.FromResult((TReturn)result);
    }

    private static byte[] ReadStream(Stream stream)
    {
        if (stream is MemoryStream memory) return memory.ToArray();
        using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray();
    }
}
