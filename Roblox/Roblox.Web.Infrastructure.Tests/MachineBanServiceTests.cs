using System.Net.NetworkInformation;
using Npgsql;
using Roblox.Dto.Admin;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.AdminApi;

namespace Roblox.Web.Infrastructure.Tests;

public class MachineBanServiceTests
{
    [Fact]
    public async Task ExactMacMatch_SchedulesOnceWithinDelayWindow_AndRegistrySurvivesDeletion()
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
            return;

        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        using var machineBans = Roblox.Services.ServiceProvider.GetOrCreate<MachineBanService>();
        var source = await users.CreateUser(NewUsername("MachineSource"), "password123", Gender.Male);
        var firstAlt = await users.CreateUser(NewUsername("MachineAltA"), "password123", Gender.Male);
        var secondAlt = await users.CreateUser(NewUsername("MachineAltB"), "password123", Gender.Male);
        var enforcedAlt = await users.CreateUser(NewUsername("MachineEnforced"), "password123", Gender.Male);
        var cleanUser = await users.CreateUser(NewUsername("MachineClean"), "password123", Gender.Male);
        var realMacSeed = Guid.NewGuid().ToByteArray();
        var realMac = new PhysicalAddress(new byte[]
        {
            0xA2, realMacSeed[0], realMacSeed[1], realMacSeed[2], realMacSeed[3], realMacSeed[4],
        });
        var excludedMac = PhysicalAddress.Parse("0A00270000FF");

        await machineBans.ActivateAsync(
            source.userId,
            1,
            PermanentAccountTerminationService.GenericTermsOfServiceReason);

        var excluded = await machineBans.RecordAndScheduleAsync(source.userId, new[] { excludedMac });
        Assert.False(excluded.IsMatch);

        var sourceDetection = await machineBans.RecordAndScheduleAsync(source.userId, new[] { realMac });
        Assert.True(sourceDetection.IsMatch);
        Assert.True(sourceDetection.JobCreated);
        Assert.Equal(source.userId, sourceDetection.SourceUserId);

        var firstDue = await ReadDueTimeAsync(source.userId);
        Assert.InRange(firstDue.ExecuteAt - firstDue.CreatedAt, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(26));

        var duplicate = await machineBans.RecordAndScheduleAsync(source.userId, new[] { realMac });
        Assert.True(duplicate.IsMatch);
        Assert.False(duplicate.JobCreated);
        var duplicateDue = await ReadDueTimeAsync(source.userId);
        Assert.Equal(firstDue.ExecuteAt, duplicateDue.ExecuteAt);

        var altDetection = await machineBans.RecordAndScheduleAsync(firstAlt.userId, new[] { realMac });
        Assert.True(altDetection.IsMatch);
        Assert.True(altDetection.JobCreated);
        Assert.Equal(source.userId, altDetection.SourceUserId);

        var enforceDetection = await machineBans.RecordAndScheduleAsync(enforcedAlt.userId, new[] { realMac });
        Assert.True(enforceDetection.JobCreated);
        await ExecuteAsync(
            "UPDATE machine_ban_enforcement SET execute_at = CURRENT_TIMESTAMP - INTERVAL '1 second' WHERE user_id = @id",
            ("id", enforcedAlt.userId));
        Assert.True(await machineBans.TryProcessNextAsync());
        Assert.Equal(AccountStatus.Deleted, (await users.GetUserById(enforcedAlt.userId)).accountStatus);
        Assert.Equal(1L, await ReadCountAsync(
            "SELECT COUNT(*) FROM user_ban WHERE user_id = @id", enforcedAlt.userId));
        Assert.Equal(1L, await ReadCountAsync(
            "SELECT COUNT(*) FROM moderation_ban WHERE user_id = @id", enforcedAlt.userId));
        Assert.Equal(1L, await ReadCountAsync(
            "SELECT COUNT(*) FROM machine_ban_enforcement WHERE user_id = @id AND completed_at IS NOT NULL", enforcedAlt.userId));

        await ExecuteAsync(
            "UPDATE \"user\" SET status = @status WHERE id = @id",
            ("status", (object)(int)AccountStatus.Deleted),
            ("id", source.userId));
        var afterDeletion = await machineBans.RecordAndScheduleAsync(secondAlt.userId, new[] { realMac });
        Assert.True(afterDeletion.IsMatch);
        Assert.Equal(source.userId, afterDeletion.SourceUserId);

        await machineBans.UnbanAsync(source.userId, 1);
        Assert.Equal(0L, await ReadCountAsync(
            "SELECT COUNT(*) FROM machine_ban_enforcement WHERE source_user_id = @id", source.userId));
        var afterUnban = await machineBans.RecordAndScheduleAsync(cleanUser.userId, new[] { realMac });
        Assert.False(afterUnban.IsMatch);

        await machineBans.ActivateAsync(source.userId, 1, "reactivated test machine ban");
        var afterReactivation = await machineBans.RecordAndScheduleAsync(firstAlt.userId, new[] { realMac });
        Assert.True(afterReactivation.JobCreated);
    }

    [Fact]
    public async Task AdminIdentitySearch_ScoresMacEvidenceAndIpOnlyBoostsMacCandidates()
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
            return;

        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        using var admin = Roblox.Services.ServiceProvider.GetOrCreate<AdminApiService>();
        var source = await users.CreateUser(NewUsername("AltSource"), "password123", Gender.Male);
        var exact = await users.CreateUser(NewUsername("AltExact"), "password123", Gender.Male);
        var partial = await users.CreateUser(NewUsername("AltPartial"), "password123", Gender.Male);
        var ipOnly = await users.CreateUser(NewUsername("AltIpOnly"), "password123", Gender.Male);
        var macSeed = Guid.NewGuid().ToByteArray();
        var firstMac = $"A2:{macSeed[0]:X2}:{macSeed[1]:X2}:{macSeed[2]:X2}:{macSeed[3]:X2}:01";
        var secondMac = $"A2:{macSeed[0]:X2}:{macSeed[1]:X2}:{macSeed[2]:X2}:{macSeed[3]:X2}:02";
        var partialMac = $"A2:{macSeed[0]:X2}:{macSeed[1]:X2}:{macSeed[2]:X2}:{macSeed[3]:X2}:03";
        var actor = new AdminActorContext { userId = 1, isOwner = true };

        await ExecuteAsync("INSERT INTO user_mac_address (user_id, mac_address) VALUES (@id, @mac::macaddr)", ("id", source.userId), ("mac", firstMac));
        await ExecuteAsync("INSERT INTO user_mac_address (user_id, mac_address) VALUES (@id, @mac::macaddr)", ("id", source.userId), ("mac", secondMac));
        // Reversed insertion order proves set comparison is order-independent.
        await ExecuteAsync("INSERT INTO user_mac_address (user_id, mac_address) VALUES (@id, @mac::macaddr)", ("id", exact.userId), ("mac", secondMac));
        await ExecuteAsync("INSERT INTO user_mac_address (user_id, mac_address) VALUES (@id, @mac::macaddr)", ("id", exact.userId), ("mac", firstMac));
        await ExecuteAsync("INSERT INTO user_mac_address (user_id, mac_address) VALUES (@id, @mac::macaddr)", ("id", partial.userId), ("mac", firstMac));
        await ExecuteAsync("INSERT INTO user_mac_address (user_id, mac_address) VALUES (@id, @mac::macaddr)", ("id", partial.userId), ("mac", partialMac));
        foreach (var userId in new[] { source.userId, exact.userId, partial.userId, ipOnly.userId })
            await ExecuteAsync("INSERT INTO user_ip_address (user_id, ip_hash, action) VALUES (@id, 'shared-hash', 'Login')", ("id", userId));

        var result = await admin.GetUserAltAccountScoresAsync(actor, source.userId);

        Assert.Equal(2, result.sourceMacCount);
        var exactResult = Assert.Single(result.data, value => value.id == exact.userId);
        var partialResult = Assert.Single(result.data, value => value.id == partial.userId);
        Assert.DoesNotContain(result.data, value => value.id == ipOnly.userId);
        Assert.True(exactResult.exactMacSet);
        Assert.False(partialResult.exactMacSet);
        Assert.True(exactResult.score > partialResult.score);
        Assert.Equal(1, exactResult.sharedIpHashCount);

        var exactMacSearch = await admin.SearchUsersByMacAddressAsync(actor, firstMac, true);
        Assert.Contains(exactMacSearch, value => value.id == source.userId);
        Assert.Contains(exactMacSearch, value => value.id == exact.userId);
        Assert.DoesNotContain(exactMacSearch, value => value.id == partial.userId);

        await admin.SetIpBanAsync(actor, new AdminIpBanRequest { ipHash = "shared-hash", internalReason = "test reason" });
        Assert.True((await admin.GetIpBanStatusAsync(actor, "shared-hash")).isBanned);
        await admin.RevokeIpBanAsync(actor, "shared-hash");
        Assert.False((await admin.GetIpBanStatusAsync(actor, "shared-hash")).isBanned);
    }

    [Fact]
    public async Task UsernameSearch_PutsExactCaseInsensitiveMatchFirst()
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
            return;

        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        using var admin = Roblox.Services.ServiceProvider.GetOrCreate<AdminApiService>();
        var exactName = NewUsername("Exact");
        await users.CreateUser("X" + exactName, "password123", Gender.Male);
        var exact = await users.CreateUser(exactName, "password123", Gender.Male);

        var results = await admin.GetUsersAsync(limit: 10, query: exactName.ToUpperInvariant());

        Assert.Equal(exact.userId, Convert.ToInt64(results.data.First()["id"]));
    }

    private static string NewUsername(string prefix)
    {
        return prefix + Guid.NewGuid().ToString("N")[..10];
    }

    private static async Task<(DateTimeOffset ExecuteAt, DateTimeOffset CreatedAt)> ReadDueTimeAsync(long userId)
    {
        await using var connection = new NpgsqlConnection(DockerInfrastructureFixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT execute_at, created_at FROM machine_ban_enforcement WHERE user_id = @user_id",
            connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetFieldValue<DateTimeOffset>(0), reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(DockerInfrastructureFixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ReadCountAsync(string sql, long id)
    {
        await using var connection = new NpgsqlConnection(DockerInfrastructureFixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
