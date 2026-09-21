using Newtonsoft.Json;

namespace Roblox.Libraries.RobloxApi;

public class OAuthRobloxApiResponse
{
    public class RobloxUserInfo
    {
        public long id { get; set; }
        public string username { get; set; }
        public string nickname { get; set; }
        public DateTime createdAt { get; set; }
        public string profileUrl { get; set; }
        public string picture { get; set; }
    }

    public class UserInfoResponseV1
    {
        public string sub { get; set; }
        public string name { get; set; }
        public string nickname { get; set; }
        [JsonProperty("preferred_username")]
        public string preferred_username { get; set; }
        public string created_at { get; set; }
        public string profile { get; set; }
        public string picture { get; set; }
    }

    public class TokenResponseV1
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string scope { get; set; }
    }
}