using System.Security.Cryptography;

namespace Roblox.Services;

public sealed class SessionNegotiationTicketService : ServiceBase, IService
{
    private const int TicketByteLength = 32;
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);

    public async Task<string> IssueAsync(string sessionToken, string ip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);

        var bytes = new byte[TicketByteLength];
        RandomNumberGenerator.Fill(bytes);
        var ticket = Convert.ToHexString(bytes).ToLowerInvariant();
        await redis.StringSetAsync(GetRedisKey(ticket, ip), sessionToken, TicketLifetime);
        return ticket;
    }

    public async Task<string?> ConsumeAsync(string? ticket, string ip)
    {
        if (!IsValidTicket(ticket))
        {
            return null;
        }

        return await redis.StringGetDeleteAsync(GetRedisKey(ticket!, ip));
    }

    private static bool IsValidTicket(string? ticket)
    {
        if (ticket is null || ticket.Length != TicketByteLength * 2)
        {
            return false;
        }

        return ticket.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static string GetRedisKey(string ticket, string ip) => $"session:negotiate:v1:{ip}:{ticket}";

    public bool IsThreadSafe() => true;
    public bool IsReusable() => false;
}
