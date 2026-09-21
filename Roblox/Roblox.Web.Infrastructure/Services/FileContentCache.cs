using System.Collections.Concurrent;

namespace Roblox.Web.Infrastructure.Services;

public sealed class FileContentCache
{
    private readonly ConcurrentDictionary<string, CachedFile> _cachedFiles = new(StringComparer.OrdinalIgnoreCase);

    public string ReadText(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath);

        if (_cachedFiles.TryGetValue(fullPath, out var cached) && cached.LastWriteTimeUtc == lastWriteTimeUtc)
        {
            return cached.Content;
        }

        var updated = new CachedFile(lastWriteTimeUtc, File.ReadAllText(fullPath));
        _cachedFiles[fullPath] = updated;
        return updated.Content;
    }

    private sealed record CachedFile(DateTime LastWriteTimeUtc, string Content);
}
