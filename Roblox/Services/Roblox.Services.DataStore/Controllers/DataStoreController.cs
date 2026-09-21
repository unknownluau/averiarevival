using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Assets;
using Roblox.Dto.Persistence;
using Roblox.Logging;
using Roblox.Services;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using ServiceProvider = Roblox.Services.ServiceProvider;

namespace Roblox.Services.DataStore.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("/")]
public class DataStoreController : RobloxControllerBase
{
    [HttpPost("persistence/set")]
    public async Task<dynamic> Set(
        long placeId,
        string key,
        string type,
        string scope,
        string target,
        int valueLength)
    {
        if (!isRCC)
        {
            throw new RobloxException(400, 0, "BadRequest");
        }

        var value = Request.Form["value"][0]!;
        if (type is not "standard")
        {
            long.Parse(value);
        }

        placeId = await NormalizePlaceId(placeId);
        await ServiceProvider.GetOrCreate<DataStoreService>()
            .Set(placeId, key, type, scope, target, valueLength, value);
        return new
        {
            data = new
            {
                Value = value,
                Scope = scope,
                Key = key,
                Target = target,
            },
        };
    }

    [HttpPost("persistence/increment")]
    public async Task<dynamic> Increment(
        long placeId,
        string key,
        string type,
        string scope,
        string target,
        long? value = null)
    {
        if (!isRCC)
        {
            throw new RobloxException(400, 0, "BadRequest");
        }

        var ds = ServiceProvider.GetOrCreate<DataStoreService>();
        placeId = await NormalizePlaceId(placeId);
        if (value == null)
        {
            value = int.Parse(Request.Form["value"][0]!);
        }

        var current = await ds.Get(placeId, key, type, scope, target);
        if (current is null)
        {
            throw new RobloxException(404, 0, "KeyNotFound");
        }

        var oldValue = long.Parse(current);
        await ds.Increment(placeId, key, type, scope, target, value.Value);
        var updated = await ds.Get(placeId, key, type, scope, target);
        var newValue = long.Parse(updated!);

        Writer.Info(LogGroup.DataStoreService, $"Incremented {key} from {oldValue} to {newValue} for placeId {placeId}, scope {scope}, target {target}");
        return new
        {
            data = newValue,
        };
    }

    [Consumes("application/x-www-form-urlencoded")]
    [HttpPost("persistence/getv2")]
    public async Task<dynamic> BatchGet(
        long placeId,
        string type,
        string scope,
        [FromForm] QueuedKeysRequest request)
    {
        if (!isRCC)
        {
            throw new RobloxException(403, 0, "Unauthorized");
        }

        using var ds = ServiceProvider.GetOrCreate<DataStoreService>();
        placeId = await NormalizePlaceId(placeId);
        var result = await ds.MultiGetDataStores(placeId, type, scope, request.qkeys);
        return new
        {
            data = result.Select(c => new KeyEntry
            {
                Key = c.key,
                Scope = c.scope,
                Target = c.name,
                Value = type != "standard" ? Convert.ToInt64(c.value) : c.value!,
            }),
        };
    }

    [HttpPost("persistence/getSortedValues")]
    public async Task<dynamic> Sorted(
        long placeId,
        string type,
        string scope,
        string key,
        bool ascending,
        int pageSize = 50,
        long? inclusiveMinValue = 0,
        long? inclusiveMaxValue = 0,
        string? exclusiveStartKey = null)
    {
        using var ds = ServiceProvider.GetOrCreate<DataStoreService>();
        placeId = await NormalizePlaceId(placeId);
        if (!isRCC)
        {
            throw new RobloxException(403, 0, "BadRequest");
        }

        if (pageSize > 100)
        {
            throw new RobloxException(400, 0, "PageSizeTooLarge");
        }

        if (type != "sorted")
        {
            throw new RobloxException(400, 0, "TypeNotSorted");
        }

        var result = await ds.GetAllOrderedEntries(
            placeId,
            key,
            scope,
            ascending,
            pageSize,
            inclusiveMinValue,
            inclusiveMaxValue,
            exclusiveStartKey);

        if (result.Count() >= pageSize)
        {
            var last = result.Last();
            exclusiveStartKey = $"{last.name}${last.value}";
        }

        return new
        {
            data = new
            {
                Entries = result.Select(c => new KeyEntry
                {
                    Target = c.name,
                    Value = c.value,
                }),
                ExclusiveStartKey = exclusiveStartKey,
            },
        };
    }

    private async Task<long> NormalizePlaceId(long placeId)
    {
        var universeId = await services.games.GetUniverseId(placeId);
        return await services.games.GetRootPlaceId(universeId);
    }
}
