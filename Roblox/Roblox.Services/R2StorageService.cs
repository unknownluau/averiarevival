using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Roblox.Services;

public class R2StorageService : ServiceBase, IService
{
    public record R2ObjectMetadata(string Key, DateTime LastModified, long ContentLength, string? ContentType);
    
    private readonly AmazonS3Client _client;

    public R2StorageService()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{Configuration.R2AccountId}.r2.cloudflarestorage.com",
        };
        _client = new AmazonS3Client(Configuration.R2AccessKey, Configuration.R2SecretKey, config);
    }

    public string GetPrefixFromLocalDirectory(string? directory)
    {
        if (directory == Configuration.ThumbnailsDirectory) return "images/thumbnails/";
        if (directory == Configuration.GroupIconsDirectory) return "images/groups/";
        return "assets/";
    }

    public async Task UploadFileAsync(string key, Stream content, string contentType = "application/octet-stream")
    {
        var request = new PutObjectRequest
        {
            BucketName = Configuration.R2BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true
        };
        await _client.PutObjectAsync(request);
    }

    public async Task<Stream?> GetFileAsync(string key)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = Configuration.R2BucketName,
                Key = key
            };
            var response = await _client.GetObjectAsync(request);
            var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    public async Task<IEnumerable<string>> ListFilesAsync(string prefix)
    {
        var keys = new List<string>();
        string? continuationToken = null;
 
        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = Configuration.R2BucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken,
            };
            var response = await _client.ListObjectsV2Async(request);
            keys.AddRange(response.S3Objects.Select(o => o.Key));
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken is not null);
 
        return keys;
    }

    public async Task DeleteFileAsync(string key)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = Configuration.R2BucketName,
                Key = key
            };
            await _client.DeleteObjectAsync(request);
        }
        catch (AmazonS3Exception)
        {
            // if missing, say bismillah and ignore.
        }
    }
    

    public async Task<bool> FileExistsAsync(string key)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = Configuration.R2BucketName,
                Key = key
            };
            await _client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
    
    public async Task<R2ObjectMetadata?> GetFileMetadataAsync(string key)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = Configuration.R2BucketName,
                Key = key
            };
            var response = await _client.GetObjectMetadataAsync(request);
            return new R2ObjectMetadata(
                Key:           key,
                LastModified:  response.LastModified ?? DateTime.MinValue,
                ContentLength: response.ContentLength,
                ContentType:   response.Headers.ContentType
            );
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    
    // ── Signed URL generation (replaces GetPresignedUrl for /assets/*) ────────
    //
    // Produces:  https://{CdnBaseUrl}/{key}?expires={unix}&sig={hex-hmac}
    //
    // The Cloudflare Worker validates:
    //   HMAC-SHA256( key=HMAC_SECRET, msg="/{key}:{expires}" )  == sig
    //
    // Use this for anything under assets/. For public paths (images/) keep
    // using GetPublicUrl() — no signing needed there.

    public string GenerateSignedUrl(string key, TimeSpan expiresIn)
    {
        var expires = DateTimeOffset.UtcNow.Add(expiresIn).ToUnixTimeSeconds();

        // Must match the message the Worker verifies: "{pathname}:{expires}"
        // pathname = "/" + key  (Worker does url.pathname which includes the leading slash)
        var message = $"/{key}:{expires}";
        var sig = ComputeHmacSha256Hex(message, Configuration.HmacSecret);

        var baseUrl = Configuration.CdnBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{key}?expires={expires}&sig={sig}";
    }
    
    /// <summary>
    /// Builds the full asset key from a local directory + filename, then signs it.
    /// Mirrors how UploadFileAsync callers typically construct keys.
    /// </summary>
    public string GenerateSignedUrlForFile(string? directory, string filename, TimeSpan expiresIn)
    {
        var prefix = GetPrefixFromLocalDirectory(directory);
        var key = $"{prefix}{filename}";
        return GenerateSignedUrl(key, expiresIn);
    }

    // ── Static helper (also usable for offline verification / tests) ──────────

    public static string ComputeHmacSha256Hex(string message, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var hash = HMACSHA256.HashData(keyBytes, msgBytes);
        // Lower-case hex — Worker uses parseInt(hex, 16) which is case-insensitive,
        // but keeping it consistent avoids any future surprises.
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Validates a signed URL locally (useful in tests or middleware).
    /// Returns false if the URL is malformed, expired, or the signature is wrong.
    /// </summary>
    public static bool ValidateSignedUrl(string signedUrl, string secret)
    {
        if (!Uri.TryCreate(signedUrl, UriKind.Absolute, out var uri))
            return false;

        var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var expiresStr = qs["expires"];
        var sig = qs["sig"];

        if (string.IsNullOrEmpty(expiresStr) || string.IsNullOrEmpty(sig))
            return false;

        if (!long.TryParse(expiresStr, out var expires))
            return false;

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
            return false;

        var message = $"{uri.AbsolutePath}:{expires}";
        var expected = ComputeHmacSha256Hex(message, secret);

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(sig)
        );
    }

    public string GetPresignedUrl(string key, TimeSpan expiresIn)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Configuration.R2BucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiresIn),
            Verb = HttpVerb.GET
        };
        return _client.GetPreSignedURL(request);
    }
    
    public static string GetPublicUrl(string key)
    {
        return $"{Configuration.CdnBaseUrl.Trim('/')}/{key}";
    }
    
    public bool IsThreadSafe() => true;
    public bool IsReusable() => true;
}