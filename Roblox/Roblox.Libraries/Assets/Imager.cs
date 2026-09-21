using NetVips;

namespace Roblox.Libraries;

public enum ImagerFormat
{
    Undefined = 0,
    PNG = 1,
    JPEG,
    GIF,
    BMP,
}

public class UnsupportedImageFormatException : Exception
{

}

/// <summary>
/// Generic parse error, e.g. image format is not supported, image is corrupted, image is invalid
/// </summary>
public class InvalidImageException : Exception
{

}

public class Imager
{
    private Stream content { get; set; }

    public int height { get; private set; } = 0;
    public int width { get; private set; } = 0;
    public ImagerFormat imageFormat { get; private set; } = ImagerFormat.Undefined;

    private Image? image { get; set; }

    private Imager(Stream content)
    {
        this.content = content;
    }

    private Task InitializeAsync()
    {
        Image imageData;
        ImagerFormat decodedFormat;
        try
        {
            var loader = Image.FindLoadStream(content);
            if (content.CanSeek)
                content.Position = 0;

            imageData = Image.NewFromStream(content, "", access: Enums.Access.Sequential, failOn: Enums.FailOn.Error);
            decodedFormat = GetImageFormat(loader);
        }
        catch (VipsException)
        {
            throw new InvalidImageException();
        }

        this.image = imageData;
        height = imageData.Height;
        width = imageData.Width;
        imageFormat = decodedFormat;
        return Task.CompletedTask;
    }

    public static async Task<Imager> ReadAsync(Stream content)
    {
        var img = new Imager(content);
        await img.InitializeAsync();
        return img;
    }

    private static ImagerFormat GetImageFormat(string? loader)
    {
        if (loader == null)
            throw new InvalidImageException();

        if (loader.Contains("png", StringComparison.OrdinalIgnoreCase))
            return ImagerFormat.PNG;
        if (loader.Contains("jpeg", StringComparison.OrdinalIgnoreCase))
            return ImagerFormat.JPEG;
        if (loader.Contains("gif", StringComparison.OrdinalIgnoreCase))
            return ImagerFormat.GIF;
        if (loader.Contains("bmp", StringComparison.OrdinalIgnoreCase))
            return ImagerFormat.BMP;

        throw new UnsupportedImageFormatException();
    }
}
