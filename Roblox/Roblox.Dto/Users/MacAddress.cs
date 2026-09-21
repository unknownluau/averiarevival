using System.Net.NetworkInformation;
using Roblox.Models.Users;

namespace Roblox.Dto.Users;

public class UserMacAddressEntry
{
    public long userId { get; set; }
    public string macAddress { get; set; }
    public DateTimeOffset createdAt { get; set; }
    public DateTimeOffset updatedAt { get; set; }
}