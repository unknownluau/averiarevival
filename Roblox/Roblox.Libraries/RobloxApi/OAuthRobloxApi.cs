using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Roblox.Logging;
using static Roblox.Libraries.RobloxApi.OAuthRobloxApiResponse;

namespace Roblox.Libraries.RobloxApi;

public class OAuthRobloxApi
{
    private RobloxHttpClient robloxClient;

    public string accessToken { get; set; }
    public string refreshToken { get; set; }
    public int expiresIn { get; set; }
    public string redirectUri { get; set; }

    private class RobloxHttpClient : HttpClient
    {
        public RobloxHttpClient(string? auth) : base(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            DefaultRequestHeaders.Add("Authorization", "Bearer " + auth);
            DefaultRequestHeaders.Add("Accept", "*/*");
            DefaultRequestHeaders.Add("User-Agent", "Roblox/WinInet");
            DefaultRequestHeaders.Add("Roblox-Browser-Asset-Request", "false");
            DefaultRequestHeaders.Add("Roblox-Place-Id", "1818");
        }
        public void ChangeAuthHeader(string token)
        {
            if (DefaultRequestHeaders.Contains("Authorization"))
            {
                DefaultRequestHeaders.Remove("Authorization");
            }
            DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        }
    }

    public OAuthRobloxApi(string code, bool isToken, string redirectUri)
    {
        robloxClient = new(isToken ? code : null);
        if (!isToken)
        {
            try
            {
                var robloxToken = RequestAccessToken(code).Result;
                accessToken = robloxToken.access_token;
                refreshToken = robloxToken.refresh_token;
                expiresIn = robloxToken.expires_in;
                this.redirectUri = redirectUri;
                robloxClient.ChangeAuthHeader(accessToken);
            }
            catch (Exception ex)
            {
                Writer.Info(LogGroup.RobloxApi, "Error while authenticating code: {0}", ex.Message);
                throw;
            }
        }
        else
        {
            accessToken = code;
        }
    }

    public async Task<TokenResponseV1> RequestAccessToken(string code)
    {
        using var cancel = new CancellationTokenSource();
        cancel.CancelAfter(TimeSpan.FromSeconds(5));

        var values = new Dictionary<string, string> {
            { "client_id", Configuration.RobloxClientId.ToString() },
            { "client_secret", Configuration.RobloxClientSecret },
            { "grant_type", "authorization_code" },
            { "code", code },
            // { "code_verifier", "" }, TODO: add pkce to ROBLOX oauth
        };

        var result = await robloxClient.PostAsync("https://apis.roblox.com/oauth/v1/token",
            new FormUrlEncodedContent(values), cancel.Token);
        if (!result.IsSuccessStatusCode)
        {
            throw new Exception("Unexpected response from Roblox: " + result.StatusCode);
        }
        var str = await result.Content.ReadAsStringAsync(cancel.Token);
        return JsonConvert.DeserializeObject<TokenResponseV1>(str) ?? throw new Exception("Null json returned from OAuth authorization api");
    }

    public async Task<RobloxUserInfo?> GetUserInfo()
    {
        using var cancel = new CancellationTokenSource();
        cancel.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var result = await robloxClient.GetAsync("https://apis.roblox.com/oauth/v1/userinfo", cancel.Token);
            if (!result.IsSuccessStatusCode)
            {
                throw new Exception("Unexpected response from Roblox: " + result.StatusCode);
            }

            var str = await result.Content.ReadAsStringAsync(cancel.Token);
            var json = JsonConvert.DeserializeObject<UserInfoResponseV1>(str);
            if (json == null)
                throw new Exception("Null json returned from OAuth userinfo api");
            var jsonObject = JObject.Parse(str);
            var usernameToken = jsonObject["preferred_username"];
            if (usernameToken == null)
            {
                throw new Exception("Null json returned from OAuth userinfo api 1");
            }
            var username = usernameToken.Value<string>();
            if (username == null)
            {
                throw new Exception("Null json returned from OAuth userinfo api 1");
            }
            var userInfo = new RobloxUserInfo
            {
                id = long.Parse(json.sub),
                username = username, // i dont know why but json.preferred_username just doesnt work
                nickname = json.nickname,
                picture = json.picture,
                profileUrl = json.profile,
                createdAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(json.created_at)).DateTime,
            };
            return userInfo;
        }
        catch (Exception ex)
        {
            Writer.Info(LogGroup.RobloxApi, "GetUserInfo failed, message: " + ex.Message);
            return null;
        }
    }

    public async Task<bool> IsValid()
    {
        return await GetUserInfo() != null;
    }
}