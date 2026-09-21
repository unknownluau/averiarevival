using Dapper;
using Roblox.Libraries.DiscordApi;
using Roblox.Logging;
using Roblox.Models.Users;

namespace Roblox.Services;

public enum DiscordTerminationOutcome
{
    Banned,
    SkippedInternalReasonMismatch,
    NoRegisteredApplication,
    NotConfigured,
    Failed,
}

public sealed record PermanentTerminationResult(
    string Username,
    bool AccountChanged,
    string? DiscordId,
    DiscordTerminationOutcome DiscordOutcome);

public class PermanentAccountTerminationService : ServiceBase
{
    public const string GenericTermsOfServiceReason =
        "This account has been closed due to violating Averia terms of service.";

    private UsersService? _users;
    private GameServerService? _gameServer;
    private DiscordBotApi? _discordBotApi;

    private UsersService users => _users ??= ServiceProvider.GetOrCreate<UsersService>(this);
    private GameServerService gameServer => _gameServer ??= ServiceProvider.GetOrCreate<GameServerService>(this);
    private DiscordBotApi discordBotApi => _discordBotApi ??= new DiscordBotApi(Roblox.Configuration.DiscordBotToken);

    public async Task<PermanentTerminationResult> TerminateAsync(
        long userId,
        long actorUserId,
        string reason,
        string? internalReason)
    {
        var termination = await InTransaction(async _ =>
        {
            var user = await db.QuerySingleOrDefaultAsync<TerminationUserRow>(
                "SELECT username, status FROM \"user\" WHERE id = :id FOR UPDATE",
                new { id = userId });
            if (user == null)
                throw new InvalidOperationException($"Cannot terminate missing user {userId}.");

            var existingBanId = await db.QuerySingleOrDefaultAsync<long?>(
                "SELECT id FROM user_ban WHERE user_id = :user_id ORDER BY id DESC LIMIT 1",
                new { user_id = userId });
            if (existingBanId == null)
            {
                await db.ExecuteAsync(
                    @"INSERT INTO user_ban (user_id, reason, author_user_id, expired_at, internal_reason)
                      VALUES (:user_id, :reason, :author, NULL, :internal_reason)",
                    new
                    {
                        user_id = userId,
                        author = actorUserId,
                        reason,
                        internal_reason = internalReason,
                    });
            }
            else
            {
                await db.ExecuteAsync(
                    @"UPDATE user_ban
                      SET reason = :reason, author_user_id = :author, expired_at = NULL,
                          internal_reason = :internal_reason, updated_at = CURRENT_TIMESTAMP
                      WHERE id = :id",
                    new { id = existingBanId, author = actorUserId, reason, internal_reason = internalReason });
            }
            await db.ExecuteAsync(
                @"INSERT INTO moderation_ban (user_id, actor_id, reason, internal_reason, expired_at)
                  SELECT :user_id, :author, :reason, :internal_reason, NULL
                  WHERE NOT EXISTS (
                      SELECT 1 FROM moderation_ban
                      WHERE user_id = :user_id AND actor_id = :author AND reason = :reason
                        AND internal_reason IS NOT DISTINCT FROM :internal_reason AND expired_at IS NULL
                  )",
                new
                {
                    user_id = userId,
                    author = actorUserId,
                    reason,
                    internal_reason = internalReason,
                });
            await db.ExecuteAsync(
                @"UPDATE ""user""
                  SET status = :status, session_expired_at = CURRENT_TIMESTAMP
                  WHERE id = :id",
                new { status = AccountStatus.Deleted, id = userId });
            await db.ExecuteAsync(
                "UPDATE user_asset SET price = 0 WHERE price != 0 AND user_id = :user_id",
                new { user_id = userId });

            return new TerminationDatabaseResult(user.username, user.status != AccountStatus.Deleted);
        });

        await users.InvalidateUserInfoCache(userId);

        string? discordId = null;
        DiscordTerminationOutcome discordOutcome;
        if (!ShouldBanDiscord(reason, internalReason))
        {
            discordOutcome = DiscordTerminationOutcome.SkippedInternalReasonMismatch;
        }
        else
        {
            discordId = await users.GetApprovedApplicationDiscordId(userId);
            discordOutcome = await BanDiscordAsync(userId, discordId);
        }

        try
        {
            await gameServer.KickPlayer(userId);
        }
        catch (Exception exception)
        {
            // An offline player has no asset_server_player row, and an unavailable
            // arbiter must not undo or indefinitely retry a durable termination.
            Writer.Info(
                LogGroup.GameServerJoin,
                "Could not kick terminated Averia user {0}: {1}",
                userId,
                exception.Message);
        }

        return new PermanentTerminationResult(
            termination.Username,
            termination.AccountChanged,
            discordId,
            discordOutcome);
    }

    public static bool ShouldBanDiscord(string publicReason, string? internalReason)
    {
        return string.Equals(internalReason, publicReason, StringComparison.Ordinal);
    }

    private async Task<DiscordTerminationOutcome> BanDiscordAsync(long userId, string? discordId)
    {
        if (string.IsNullOrWhiteSpace(discordId))
        {
            Writer.Info(
                LogGroup.DiscordApi,
                "Cannot Discord-ban terminated Averia user {0}: no approved registered application Discord ID",
                userId);
            return DiscordTerminationOutcome.NoRegisteredApplication;
        }

        if (string.IsNullOrWhiteSpace(Roblox.Configuration.DiscordBotToken) ||
            string.IsNullOrWhiteSpace(Roblox.Configuration.DiscordGuildId))
        {
            Writer.Info(
                LogGroup.DiscordApi,
                "Discord termination is not configured for Averia user {0} (Discord {1})",
                userId,
                discordId);
            return DiscordTerminationOutcome.NotConfigured;
        }

        try
        {
            return await discordBotApi.BanGuildMember(
                Roblox.Configuration.DiscordGuildId,
                discordId,
                $"Permanent Averia termination for user {userId}")
                ? DiscordTerminationOutcome.Banned
                : DiscordTerminationOutcome.Failed;
        }
        catch (Exception exception)
        {
            Writer.Info(
                LogGroup.DiscordApi,
                "Discord termination threw for Averia user {0} (Discord {1}): {2}",
                userId,
                discordId,
                exception.Message);
            return DiscordTerminationOutcome.Failed;
        }
    }

    private sealed class TerminationUserRow
    {
        public string username { get; init; } = string.Empty;
        public AccountStatus status { get; init; }
    }

    private sealed record TerminationDatabaseResult(string Username, bool AccountChanged);
}
