using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Dapper;
using Roblox.Libraries.DiscordApi;
using Roblox.Logging;
using Roblox.Models.Users;

namespace Roblox.Services;

public sealed record MachineBanDetectionResult(bool IsMatch, bool JobCreated, long? SourceUserId);

public class MachineBanService : ServiceBase
{
    private static readonly TimeSpan EnforcementLockDuration = TimeSpan.FromMinutes(2);

    private UsersService? _users;
    private PermanentAccountTerminationService? _termination;
    private DiscordBotApi? _discordBotApi;

    private UsersService users => _users ??= ServiceProvider.GetOrCreate<UsersService>(this);
    private PermanentAccountTerminationService termination =>
        _termination ??= ServiceProvider.GetOrCreate<PermanentAccountTerminationService>(this);
    private DiscordBotApi discordBotApi => _discordBotApi ??= new DiscordBotApi(Roblox.Configuration.DiscordBotToken);

    public async Task ActivateAsync(long userId, long actorUserId, string? internalReason)
    {
        await InTransaction<object?>(async _ =>
        {
            await db.ExecuteAsync(
                "UPDATE \"user\" SET status = :status WHERE id = :id",
                new { status = AccountStatus.MachineBanned, id = userId });
            await db.ExecuteAsync(
                @"INSERT INTO user_machine_ban
                    (user_id, actor_user_id, internal_reason, created_at, updated_at, revoked_at)
                  VALUES
                    (:user_id, :actor_user_id, :internal_reason, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, NULL)
                  ON CONFLICT (user_id) DO UPDATE SET
                    actor_user_id = EXCLUDED.actor_user_id,
                    internal_reason = EXCLUDED.internal_reason,
                    updated_at = CURRENT_TIMESTAMP,
                    revoked_at = NULL",
                new
                {
                    user_id = userId,
                    actor_user_id = actorUserId,
                    internal_reason = internalReason,
                });
            await db.ExecuteAsync(
                @"DELETE FROM machine_ban_enforcement
                  WHERE source_user_id = :user_id OR user_id = :user_id",
                new { user_id = userId });
            return null;
        });

        await users.InvalidateUserInfoCache(userId);
    }

    public async Task UnbanAsync(long userId, long actorUserId)
    {
        await InLock($"MachineBanEnforcement:V1:{userId}", EnforcementLockDuration, async () =>
        {
            await InTransaction<object?>(async _ =>
            {
                var status = await db.QuerySingleOrDefaultAsync<AccountStatus?>(
                    "SELECT status FROM \"user\" WHERE id = :id FOR UPDATE",
                    new { id = userId });
                if (status == null)
                    throw new InvalidOperationException($"Cannot unban missing user {userId}.");
                if (status == AccountStatus.Forgotten)
                    throw new InvalidOperationException("Forgotten accounts cannot be un-banned");

                await db.ExecuteAsync(
                    "UPDATE \"user\" SET status = :status WHERE id = :id",
                    new { status = AccountStatus.Ok, id = userId });
                await db.ExecuteAsync(
                    @"UPDATE user_machine_ban
                      SET revoked_at = CURRENT_TIMESTAMP, updated_at = CURRENT_TIMESTAMP
                      WHERE user_id = :user_id AND revoked_at IS NULL",
                    new { user_id = userId });
                await db.ExecuteAsync(
                    @"DELETE FROM machine_ban_enforcement
                      WHERE source_user_id = :user_id OR user_id = :user_id",
                    new { user_id = userId });
                await db.ExecuteAsync(
                    "INSERT INTO moderation_unban (user_id, actor_id) VALUES (:user_id, :actor_id)",
                    new { user_id = userId, actor_id = actorUserId });
                await db.ExecuteAsync(
                    "DELETE FROM user_ban WHERE user_id = :user_id",
                    new { user_id = userId });
                return null;
            });

            await users.InvalidateUserInfoCache(userId);
            return true;
        });
    }

    public async Task<MachineBanDetectionResult> RecordAndScheduleAsync(
        long userId,
        IReadOnlyCollection<PhysicalAddress> macAddresses)
    {
        if (macAddresses.Count == 0)
            return new MachineBanDetectionResult(false, false, null);

        var macs = macAddresses.Select(address => address.ToString()).Distinct(StringComparer.Ordinal).ToArray();
        await db.ExecuteAsync(
            @"INSERT INTO user_mac_address (user_id, mac_address, created_at, updated_at)
              SELECT :user_id, supplied.mac::macaddr, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
              FROM unnest(:macs::text[]) AS supplied(mac)
              ON CONFLICT (user_id, mac_address)
              DO UPDATE SET updated_at = EXCLUDED.updated_at",
            new { user_id = userId, macs });

        var matchableMacs = macAddresses
            .Where(address => !IsExcludedMachineBanAddress(address))
            .Select(address => address.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (matchableMacs.Length == 0)
            return new MachineBanDetectionResult(false, false, null);

        var match = await db.QuerySingleOrDefaultAsync<MachineBanMatchRow>(
            @"SELECT registry.user_id AS sourceUserId,
                     registry.actor_user_id AS actorUserId
              FROM unnest(:macs::text[]) AS supplied(mac)
              INNER JOIN user_mac_address history
                ON history.mac_address = supplied.mac::macaddr
              INNER JOIN user_machine_ban registry
                ON registry.user_id = history.user_id
               AND registry.revoked_at IS NULL
              ORDER BY registry.created_at, registry.user_id
              LIMIT 1",
            new { macs = matchableMacs });
        if (match == null)
            return new MachineBanDetectionResult(false, false, null);

        var delaySeconds = RandomNumberGenerator.GetInt32(10, 26);
        var inserted = await db.ExecuteAsync(
            @"INSERT INTO machine_ban_enforcement
                (user_id, source_user_id, actor_user_id, execute_at, created_at, updated_at)
              VALUES
                (:user_id, :source_user_id, :actor_user_id,
                 CURRENT_TIMESTAMP + (:delay_seconds * INTERVAL '1 second'),
                 CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
              ON CONFLICT (user_id) DO NOTHING",
            new
            {
                user_id = userId,
                source_user_id = match.sourceUserId,
                actor_user_id = match.actorUserId,
                delay_seconds = delaySeconds,
            });

        return new MachineBanDetectionResult(true, inserted == 1, match.sourceUserId);
    }

    public static bool IsExcludedMachineBanAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 6)
            return true;

        return HasPrefix(bytes, 0x0A, 0x00, 0x27, 0x00, 0x00) ||
               HasPrefix(bytes, 0x00, 0x1A, 0x7D, 0xDA, 0x71) ||
               HasPrefix(bytes, 0x00, 0x50, 0x56, 0xC0, 0x00) ||
               bytes.SequenceEqual(new byte[] { 0x02, 0x00, 0x4C, 0x4F, 0x4F, 0x50 });
    }

    private static bool HasPrefix(byte[] address, params byte[] prefix)
    {
        return address.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }

    public async Task<bool> TryProcessNextAsync()
    {
        var job = await ClaimNextAsync();
        if (job == null)
            return false;

        try
        {
            await InLock($"MachineBanEnforcement:V1:{job.userId}", EnforcementLockDuration, async () =>
            {
                var active = await db.QuerySingleOrDefaultAsync<bool?>(
                    @"SELECT TRUE
                      FROM machine_ban_enforcement enforcement
                      INNER JOIN user_machine_ban registry
                        ON registry.user_id = enforcement.source_user_id
                       AND registry.revoked_at IS NULL
                      WHERE enforcement.user_id = :user_id
                        AND enforcement.source_user_id = :source_user_id
                        AND enforcement.completed_at IS NULL
                      LIMIT 1",
                    new { user_id = job.userId, source_user_id = job.sourceUserId });
                if (active != true)
                {
                    await CompleteAsync(job.userId);
                    return true;
                }

                var actorUserId = job.actorUserId ?? 1;
                var internalReason = await db.QuerySingleOrDefaultAsync<string?>(
                    "SELECT internal_reason FROM user_machine_ban WHERE user_id = :source_user_id AND revoked_at IS NULL",
                    new { source_user_id = job.sourceUserId });
                var result = await termination.TerminateAsync(
                    job.userId,
                    actorUserId,
                    PermanentAccountTerminationService.GenericTermsOfServiceReason,
                    internalReason);

                await SendAutomatedTerminationLogAsync(job, actorUserId, result);
                await CompleteAsync(job.userId);
                return true;
            });
        }
        catch (Exception exception)
        {
            await ReleaseForRetryAsync(job.userId, job.attemptCount, exception);
            Writer.Info(
                LogGroup.DiscordApi,
                "Machine-ban enforcement failed for user {0} from source {1}: {2}",
                job.userId,
                job.sourceUserId,
                exception.Message);
        }

        return true;
    }

    private async Task<MachineBanEnforcementJob?> ClaimNextAsync()
    {
        return await db.QuerySingleOrDefaultAsync<MachineBanEnforcementJob>(
            @"UPDATE machine_ban_enforcement
              SET lease_until = CURRENT_TIMESTAMP + INTERVAL '60 seconds',
                  attempt_count = attempt_count + 1,
                  updated_at = CURRENT_TIMESTAMP
              WHERE user_id = (
                  SELECT user_id
                  FROM machine_ban_enforcement
                  WHERE completed_at IS NULL
                    AND execute_at <= CURRENT_TIMESTAMP
                    AND (lease_until IS NULL OR lease_until < CURRENT_TIMESTAMP)
                  ORDER BY execute_at, user_id
                  FOR UPDATE SKIP LOCKED
                  LIMIT 1
              )
              RETURNING user_id AS userId,
                        source_user_id AS sourceUserId,
                        actor_user_id AS actorUserId,
                        attempt_count AS attemptCount",
            new { });
    }

    private async Task CompleteAsync(long userId)
    {
        await db.ExecuteAsync(
            @"UPDATE machine_ban_enforcement
              SET completed_at = CURRENT_TIMESTAMP,
                  lease_until = NULL,
                  last_error = NULL,
                  updated_at = CURRENT_TIMESTAMP
              WHERE user_id = :user_id",
            new { user_id = userId });
    }

    private async Task ReleaseForRetryAsync(long userId, int attemptCount, Exception exception)
    {
        var retrySeconds = Math.Min(60, Math.Max(2, 1 << Math.Min(attemptCount, 5)));
        var error = exception.ToString();
        if (error.Length > 2048)
            error = error[..2048];

        await db.ExecuteAsync(
            @"UPDATE machine_ban_enforcement
              SET execute_at = CURRENT_TIMESTAMP + (:retry_seconds * INTERVAL '1 second'),
                  lease_until = NULL,
                  last_error = :last_error,
                  updated_at = CURRENT_TIMESTAMP
              WHERE user_id = :user_id AND completed_at IS NULL",
            new { user_id = userId, retry_seconds = retrySeconds, last_error = error });
    }

    private async Task SendAutomatedTerminationLogAsync(
        MachineBanEnforcementJob job,
        long actorUserId,
        PermanentTerminationResult result)
    {
        if (string.IsNullOrWhiteSpace(Roblox.Configuration.DiscordBotToken) ||
            string.IsNullOrWhiteSpace(Roblox.Configuration.DiscordLogChannelId))
        {
            Writer.Info(
                LogGroup.DiscordApi,
                "Automated machine termination: user {0}, source {1}, actor {2}, Discord outcome {3}",
                job.userId,
                job.sourceUserId,
                actorUserId,
                result.DiscordOutcome);
            return;
        }

        var discordIdentity = result.DiscordId == null ? "none" : result.DiscordId;
        var content =
            $"## Automated machine-ban termination\n" +
            $"Averia user: {result.Username} ({job.userId})\n" +
            $"Machine-ban source: {job.sourceUserId}\n" +
            $"Initiating staff: {actorUserId}\n" +
            $"Discord user: {discordIdentity}\n" +
            $"Discord outcome: {result.DiscordOutcome}\n" +
            $"Completed: {DateTimeOffset.UtcNow:O}";
        try
        {
            if (await discordBotApi.SendMessageInChannel(Roblox.Configuration.DiscordLogChannelId, content))
                return;
        }
        catch (Exception exception)
        {
            Writer.Info(
                LogGroup.DiscordApi,
                "Automated machine termination log threw for user {0}, source {1}: {2}",
                job.userId,
                job.sourceUserId,
                exception.Message);
        }

        Writer.Info(
            LogGroup.DiscordApi,
            "Failed to send automated machine termination log for user {0}, source {1}, actor {2}, Discord outcome {3}",
            job.userId,
            job.sourceUserId,
            actorUserId,
            result.DiscordOutcome);
    }

    private sealed class MachineBanMatchRow
    {
        public long sourceUserId { get; init; }
        public long? actorUserId { get; init; }
    }

    private sealed class MachineBanEnforcementJob
    {
        public long userId { get; init; }
        public long sourceUserId { get; init; }
        public long? actorUserId { get; init; }
        public int attemptCount { get; init; }
    }
}
