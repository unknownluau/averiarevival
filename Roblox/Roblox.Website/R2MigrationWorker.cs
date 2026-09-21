using Microsoft.Extensions.Hosting;
using Roblox.Services;
using System.Collections.Concurrent;

namespace Roblox.Website;

public class R2MigrationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public R2MigrationWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var r2Service = new R2StorageService();

        var mappings = new Dictionary<string, string>
        {
            { Configuration.AssetDirectory, "assets/" },
            { Configuration.ThumbnailsDirectory, "images/thumbnails/" },
            { Configuration.GroupIconsDirectory, "images/groups/" },
        };

        foreach (var mapping in mappings)
        {
            if (!Directory.Exists(mapping.Key)) continue;

            var tasks = new List<Task>();
            foreach (var file in Directory.GetFiles(mapping.Key).Where(f => !f.EndsWith(".migrated")))
            {
                if (stoppingToken.IsCancellationRequested) return;

                var markerPath = file + ".migrated";
                if (File.Exists(markerPath)) continue;

                var fileName = Path.GetFileName(file);
                var r2Key = mapping.Value + fileName;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var fs = File.OpenRead(file);
                        await r2Service.UploadFileAsync(r2Key, fs);
                        File.Create(markerPath).Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[R2-MIGRATION] Failed to migrate {file}: {ex.Message}");
                    }
                }, stoppingToken));

                if (tasks.Count >= 5)
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }
            }

            await Task.WhenAll(tasks);
        }
    }
}