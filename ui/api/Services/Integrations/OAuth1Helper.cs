namespace GpxAnalyzer.Api.Services.Integrations;

using System.Security.Cryptography;
using System.Text;

public static class OAuth1Helper
{
    public static void SignRequest(
        HttpRequestMessage request,
        string consumerKey,
        string consumerSecret,
        string? tokenKey = null,
        string? tokenSecret = null,
        Dictionary<string, string>? extraParams = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");

        var oauthParams = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_version"] = "1.0",
        };

        if (!string.IsNullOrEmpty(tokenKey))
            oauthParams["oauth_token"] = tokenKey;

        if (extraParams is not null)
        {
            foreach (var (key, value) in extraParams)
                oauthParams[key] = value;
        }

        // Build signature base string (RFC 5849)
        var method = request.Method.Method.ToUpperInvariant();
        var uri = request.RequestUri!;
        var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

        // Include query string params in signature base
        var allParams = new SortedDictionary<string, string>(oauthParams);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            foreach (string key in queryParams)
            {
                if (key is not null)
                    allParams[key] = queryParams[key]!;
            }
        }

        var paramString = string.Join("&",
            allParams.Select(p => $"{PercentEncode(p.Key)}={PercentEncode(p.Value)}"));

        var signatureBase = $"{method}&{PercentEncode(baseUrl)}&{PercentEncode(paramString)}";

        // HMAC-SHA1
        var signingKey = $"{PercentEncode(consumerSecret)}&{PercentEncode(tokenSecret ?? "")}";
        var hash = HMACSHA1.HashData(Encoding.ASCII.GetBytes(signingKey), Encoding.ASCII.GetBytes(signatureBase));
        var signature = Convert.ToBase64String(hash);

        oauthParams["oauth_signature"] = signature;

        // Build Authorization header
        var headerValue = string.Join(", ",
            oauthParams
                .Where(p => p.Key.StartsWith("oauth_"))
                .Select(p => $"{PercentEncode(p.Key)}=\"{PercentEncode(p.Value)}\""));

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", headerValue);
    }

    private static string PercentEncode(string value)
    {
        return Uri.EscapeDataString(value)
            .Replace("+", "%20")
            .Replace("*", "%2A")
            .Replace("%7E", "~");
    }
}
