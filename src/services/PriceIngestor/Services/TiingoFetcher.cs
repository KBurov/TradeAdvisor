using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Common.Rest;

using PriceIngestor.Domain;
using PriceIngestor.Repositories;

namespace PriceIngestor.Services;

public sealed class TiingoFetcher(
    IHttpClientFactory httpFactory,
    IDataProviderRepository dataProviderRepository,
    IConfiguration cfg,
    Serilog.ILogger logger
) : IBarFetcher
{
    private sealed record TiingoBar(
        [property: JsonPropertyName("date")] DateTimeOffset Date,
        [property: JsonPropertyName("open")] decimal? Open,
        [property: JsonPropertyName("high")] decimal? High,
        [property: JsonPropertyName("low")] decimal? Low,
        [property: JsonPropertyName("close")] decimal? Close,
        [property: JsonPropertyName("volume")] long? Volume,
        [property: JsonPropertyName("adjClose")] decimal? AdjClose
    );

    private const string TiingoApiKeyName = "TIINGO_API_KEY";
    private const int MaxRetries = 3;
    private const int MaxBackoffSeconds = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<IReadOnlyList<PriceRow>> GetDailyAsync(
        string symbol, DateOnly start, DateOnly end, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException($"{nameof(symbol)} is required", nameof(symbol));
        if (end < start)
            throw new ArgumentException($"{nameof(end)} must be >= {nameof(start)}");

        var token = (Environment.GetEnvironmentVariable(TiingoApiKeyName) ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException($"{TiingoApiKeyName} is missing.");

        var maxRetries = cfg.GetValue<int?>("Tiingo:MaxRetries") ?? MaxRetries;
        var maxBackoffSeconds = cfg.GetValue<int?>("Tiingo:MaxBackoffSeconds") ?? MaxBackoffSeconds;
        var replaceDot = cfg.GetValue<bool?>("Tiingo:ReplaceDotWithHyphen") ?? false;
        // Runtime source of truth is env/config (DB base_url is informational in our schema docs)
        var baseUrl = await dataProviderRepository.GetBaseUrlAsync(DataProvider.Tiingo, ct)
                      ?? cfg["Tiingo:BaseUrl"]
                      ?? "https://api.tiingo.com";

        var encoded = NormalizeSymbolForTiingo(symbol, replaceDot);
        var startStr = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endStr = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var url = $"{baseUrl.TrimEnd('/')}/tiingo/daily/{encoded}/prices" +
                  $"?startDate={startStr}&endDate={endStr}&format=json";

        var client = httpFactory.CreateClient("tiingo");

        // Minimal retry for 429/5xx with Retry-After support
        var attempt = 0;

        while (true)
        {
            attempt++;

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Token", token);
            req.Headers.UserAgent.ParseAdd("PriceIngestor/1.0 (+https://github.com/KBurov/TradeAdvisor)");

            HttpResponseMessage resp;

            try
            {
                resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < maxRetries)
            {
                await Task.Delay(RestClientUtils.Backoff(attempt, maxBackoffSeconds), ct);
                continue;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(RestClientUtils.Backoff(attempt, maxBackoffSeconds), ct);
                continue;
            }

            using (resp)
            {
                if (resp.IsSuccessStatusCode)
                {
                    if (resp.Headers.TryGetValues("X-RateLimit-Remaining", out var rem) &&
                        resp.Headers.TryGetValues("X-RateLimit-Limit", out var lim))
                    {
                        logger.Debug("Tiingo rate limit: {Remaining}/{Limit}", rem.FirstOrDefault(), lim.FirstOrDefault());
                    }

                    if (resp.StatusCode == HttpStatusCode.NoContent)
                        return Array.Empty<PriceRow>();

                    var ctHeader = resp.Content.Headers.ContentType;
                    if (!(ctHeader?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        logger.Warning("Unexpected content-type from Tiingo for {Symbol}: {CT}", symbol, ctHeader);
                    }

                    var bars = await RestClientUtils.DeserializeBufferedAsync<List<TiingoBar>>(resp, JsonOptions, logger, ct);
                    var result = new List<PriceRow>(bars?.Count ?? 0);

                    foreach (var b in (bars ?? Enumerable.Empty<TiingoBar>()).OrderBy(x => x.Date))
                    {
                        if (b.Open is null || b.High is null || b.Low is null || b.Close is null || b.AdjClose is null || b.Volume is null)
                        {
                            var badDate = DateOnly.FromDateTime(b.Date.UtcDateTime.Date);
                            logger.Warning("Null field from Tiingo for {Symbol} on {Date}. Aborting batch [{Start}..{End}].",
                                           symbol, badDate, startStr, endStr);
                            break;
                        }

                        result.Add(new PriceRow(
                            TradeDate: DateOnly.FromDateTime(b.Date.UtcDateTime.Date),
                            Open: b.Open.Value,
                            High: b.High.Value,
                            Low: b.Low.Value,
                            Close: b.Close.Value,
                            AdjClose: b.AdjClose.Value,
                            Volume: b.Volume.Value
                        ));

                        ct.ThrowIfCancellationRequested();
                    }

                    if (result.Count > 1)
                    {
                        result = result
                            .GroupBy(r => r.TradeDate)
                            .Select(g => g.Last())
                            .OrderBy(r => r.TradeDate)
                            .ToList();
                    }

                    if (result.Count == 0)
                        logger.Information("Tiingo returned 0 bars for {Symbol} [{Start}..{End}]", symbol, startStr, endStr);

                    logger.Debug("Tiingo OK {Symbol} [{Start}..{End}] bars={Count} attempts={Attempt}",
                                 symbol, startStr, endStr, result.Count, attempt);

                    return result;
                }

                if (RestClientUtils.IsTransient(resp.StatusCode) && attempt < maxRetries)
                {
                    var delay = RestClientUtils.ComputeRetryDelay(resp.Headers, attempt, maxBackoffSeconds);
                    logger.Warning("Tiingo transient {Status} for {Symbol}. Retry {Attempt}/{Max} in {Delay}s (Retry-After: {RetryAfter})",
                        (int)resp.StatusCode, symbol, attempt, maxRetries, delay, resp.Headers.RetryAfter?.Delta?.TotalSeconds);
                    await Task.Delay(delay, ct);
                    continue;
                }

                var body = await RestClientUtils.SafeReadStringAsync(resp, ct);
                var isAuth = resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
                logger.Error(
                    isAuth
                        ? "Tiingo auth error {Status} for {Symbol}: {Body}"
                        : "Tiingo HTTP {Status} for {Symbol}: {Body}",
                    (int)resp.StatusCode, symbol, RestClientUtils.Truncate(body, isAuth ? 512 : 1024));
                resp.EnsureSuccessStatusCode(); // throws
            }
        }
    }

    private static string NormalizeSymbolForTiingo(string raw, bool replaceDot)
    {
        var s = raw.Trim().ToUpperInvariant();
        if (replaceDot && s.Contains('.')) s = s.Replace('.', '-');
        return Uri.EscapeDataString(s);
    }
}
