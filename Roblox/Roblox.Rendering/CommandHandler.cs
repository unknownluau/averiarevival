namespace Roblox.Rendering;

public static class CommandHandler
{
    public static void Configure(string baseUrl, string authorization, bool useBinaryTransport = true) => RenderHttpClient.Configure(baseUrl, authorization, useBinaryTransport);

    private static async Task<Stream> SendAsync(RenderRequest request, CancellationToken? cancellationToken)
    {
        var result = await RenderHttpClient.SendBytesAsync(request, cancellationToken ?? CancellationToken.None);
        return new MemoryStream(result.Data, writable: false);
    }

    public static Task<Stream> RequestPlayerThumbnail(AvatarData data, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest
        {
            Kind = RenderKind.Avatar,
            UserId = data.userId,
            Avatar = data,
            AvatarRigType = string.Equals(data.playerAvatarType, "R6", StringComparison.OrdinalIgnoreCase)
                ? AvatarRigType.R6
                : AvatarRigType.R15,
            Width = 704,
            Height = 704,
        }, cancellationToken);

    public static Task<Stream> RequestPlayerHeadshot(AvatarData data, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.AvatarHeadshot, UserId = data.userId, Avatar = data, Width = 300, Height = 300 }, cancellationToken);

    public static Task<Stream> RequestTextureThumbnail(long assetId, int assetTypeId, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.Texture, AssetId = assetId, AssetTypeId = assetTypeId, Priority = RenderPriority.Background, WorkKey = $"asset:{assetId}" }, cancellationToken);

    public static Task<Stream> RequestAssetThumbnail(long assetId, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.Asset, AssetId = assetId, Priority = RenderPriority.Background, WorkKey = $"asset:{assetId}" }, cancellationToken);

    public static Task<Stream> RequestAssetMesh(long assetId, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.Mesh, AssetId = assetId, Priority = RenderPriority.Background, WorkKey = $"asset:{assetId}" }, cancellationToken);

    public static Task<Stream> RequestPlaceConversion(string base64EncodedPlace, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.PlaceConversion, InputData = base64EncodedPlace, Priority = RenderPriority.Conversion }, cancellationToken);

    public static Task<Stream> RequestHatConversion(string base64EncodedHat, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.HatConversion, InputData = base64EncodedHat, Priority = RenderPriority.Conversion }, cancellationToken);

    public static Task<Stream> RequestAssetGame(long assetId, int x, int y, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.Place, AssetId = assetId, Width = x, Height = y, Priority = RenderPriority.Background, WorkKey = $"asset:{assetId}" }, cancellationToken);

    public static Task<Stream> RequestAssetTeeShirt(long assetId, long contentId, CancellationToken? cancellationToken = null) =>
        SendAsync(new RenderRequest { Kind = RenderKind.TeeShirt, AssetId = assetId, ContentId = contentId, Priority = RenderPriority.Background, WorkKey = $"asset:{assetId}:{contentId}" }, cancellationToken);
}
