using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Common.Rest;

public static class RestClientUtils
{
    public static bool IsTransient(HttpStatusCode code) =>
        code is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)code >= 500;

    public static TimeSpan Backoff(int attempt, int maxBackoffSeconds)
    {
        var baseDelayMs = 1000 * Math.Pow(2, Math.Clamp(attempt - 1, 0, 10)); // 1s,2s,4s...
        var jitter = Random.Shared.Next(0, 250);
        var delay = TimeSpan.FromMilliseconds(baseDelayMs + jitter);
        var max = TimeSpan.FromSeconds(maxBackoffSeconds);
        return delay < max ? delay : max;
    }

    public static TimeSpan ComputeRetryDelay(HttpResponseHeaders headers, int attempt, int maxBackoffSeconds)
    {
        var ra = headers.RetryAfter;
        if (ra is not null)
        {
            if (ra.Delta is TimeSpan d) return d;
            if (ra.Date is DateTimeOffset dt)
            {
                var wait = dt - DateTimeOffset.UtcNow;
                if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                if (wait > TimeSpan.FromSeconds(maxBackoffSeconds)) wait = TimeSpan.FromSeconds(maxBackoffSeconds);
                return wait;
            }
        }
        return Backoff(attempt, maxBackoffSeconds);
    }
    public static async Task<string> SafeReadStringAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await resp.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }
    public static string Truncate(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n];

    public static async Task<T?> DeserializeBufferedAsync<T>(
        HttpResponseMessage resp, JsonSerializerOptions options, Serilog.ILogger log, CancellationToken ct)
    {
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        await src.CopyToAsync(ms, ct);
        ms.Position = 0;
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(ms, options, ct);
        }
        catch (JsonException jex)
        {
            ms.Position = 0;
            using var reader = new StreamReader(ms, leaveOpen: true);
            var body = await reader.ReadToEndAsync(ct);
            log.Error(jex, "Failed to parse JSON. Body: {Body}", Truncate(body, 1024));
            throw;
        }
    }
}
