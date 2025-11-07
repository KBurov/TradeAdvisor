using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Serilog;

using PriceIngestor.Repositories;
using PriceIngestor.Services;

public sealed class TiingoFetcherTests
{
    private static HttpClient MakeClient(HttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("http://test/") };

    private static IConfiguration MakeConfig(
        int? maxRetries = 3, int? maxBackoffSeconds = 1, bool? replaceDot = false, string? baseUrl = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Tiingo:MaxRetries"] = maxRetries?.ToString(),
            ["Tiingo:MaxBackoffSeconds"] = maxBackoffSeconds?.ToString(),
            ["Tiingo:ReplaceDotWithHyphen"] = replaceDot?.ToString(),
            ["Tiingo:BaseUrl"] = baseUrl
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    private static ILogger NullLogger() => new LoggerConfiguration().CreateLogger();

    [Fact]
    public async Task GetDaily_Success_Parses_Orders_Dedupes()
    {
        // bars out-of-order + duplicate date (second wins)
        var payload = JsonSerializer.Serialize(new[]
        {
            new { date = "2024-01-02T00:00:00Z", open=2m, high=3m, low=1m, close=2.5m, volume=100L, adjClose=2.4m },
            new { date = "2024-01-01T00:00:00Z", open=1m, high=2m, low=0.9m, close=1.5m, volume=90L, adjClose=1.4m },
            new { date = "2024-01-01T12:00:00Z", open=1.1m, high=2.1m, low=1.0m, close=1.6m, volume=95L, adjClose=1.5m }, // duplicate day, later wins
        });

        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            });

        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

        var repo = new Mock<IDataProviderRepository>();
        repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.tiingo.com");

        Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x"); // ensure token present

        var fetcher = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(), NullLogger());
        var rows = await fetcher.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), default);

        rows.Select(r => r.TradeDate).Should().BeEquivalentTo(new[]
        {
            new DateOnly(2024,1,1),
            new DateOnly(2024,1,2),
        }, opts => opts.WithStrictOrdering());

        rows.First(r => r.TradeDate == new DateOnly(2024, 1, 1)).Close.Should().Be(1.6m); // duplicate-day last wins
    }

    [Fact]
    public async Task GetDaily_AbortsOnNullField_ReturnsPartial()
    {
        var payload = JsonSerializer.Serialize(new[]
        {
            new TiingoBarDto {
                date = DateTime.Parse("2024-01-01T00:00:00Z"),
                open = 1m, high = 2m, low = 0.9m, close = 1.5m, volume = 90L, adjClose = 1.4m
            },
            new TiingoBarDto {
                date = DateTime.Parse("2024-01-02T00:00:00Z"),
                open = null, high = 2m, low = 1m, close = 1.8m, volume = 100L, adjClose = 1.7m
            },
            new TiingoBarDto {
                date = DateTime.Parse("2024-01-03T00:00:00Z"),
                open = 2m, high = 3m, low = 1m, close = 2.5m, volume = 110L, adjClose = 2.4m
            },
        });

        var handler = new QueueHandler(OkJson(payload));
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

        var repo = new Mock<IDataProviderRepository>();
        repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.tiingo.com");

        Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

        var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(), NullLogger());
        var rows = await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31), default);

        rows.Select(r => r.TradeDate).Should().Equal(new DateOnly(2024, 1, 01)); // stopped before 01-02 null
    }

    [Fact]
    public async Task GetDaily_Retries_On429_WithRetryAfter()
    {
        var first = new HttpResponseMessage((HttpStatusCode)429);
        first.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
        var second = OkJson(@"[{""date"":""2024-01-01T00:00:00Z"",""open"":1,""high"":1,""low"":1,""close"":1,""volume"":1,""adjClose"":1}]");

        var handler = new QueueHandler(first, second);
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

        var repo = new Mock<IDataProviderRepository>();
        repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.tiingo.com");

        Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

        var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(maxBackoffSeconds: 1), NullLogger());
        var rows = await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), default);

        rows.Should().HaveCount(1);
        handler.RequestCount.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetDaily_AuthErrors_Throw(HttpStatusCode code)
    {
        var handler = new QueueHandler(new HttpResponseMessage(code)
        {
            Content = new StringContent("nope")
        });

        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

        var repo = new Mock<IDataProviderRepository>();
        repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.tiingo.com");

        Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

        var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(), NullLogger());
        var act = async () => await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), default);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetDaily_204_NoContent_ReturnsEmpty()
    {
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

        var repo = new Mock<IDataProviderRepository>();
        repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.tiingo.com");

        Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

        var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(), NullLogger());
        var rows = await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), default);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDaily_Cancellation_PreCanceled_Throws()
    {
        var handler = new QueueHandler(OkJson("[]"));
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

        var repo = new Mock<IDataProviderRepository>();
        repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.tiingo.com");

        Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(), NullLogger());
        var act = async () => await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), cts.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task GetDaily_MissingToken_Throws()
    {
        Environment.SetEnvironmentVariable("TIINGO_API_KEY", null); // ensure missing

        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(new QueueHandler()));

        var repo = new Mock<IDataProviderRepository>();
        var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(), NullLogger());

        var act = async () => await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetDaily_UsesDbBaseUrl_WhenPresent_OtherwiseConfig_ElseDefault()
    {
        // Case 1: repo provides
        {
            var handler = new CapturingHandler(OkJson("[]"));
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

            var repo = new Mock<IDataProviderRepository>();
            repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://db.example");

            Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

            var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(baseUrl: "https://cfg.example"), NullLogger());
            await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), default);

            handler.LastRequest!.RequestUri!.ToString().Should().StartWith("https://db.example/tiingo/daily");
        }

        // Case 2: repo null → config
        {
            var handler = new CapturingHandler(OkJson("[]"));
            var httpFactory = new Mock<IHttpClientFactory>();
            httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

            var repo = new Mock<IDataProviderRepository>();
            repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

            var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(baseUrl: "https://cfg.example"), NullLogger());
            await f.GetDailyAsync("AAPL", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), default);

            handler.LastRequest!.RequestUri!.ToString().Should().StartWith("https://cfg.example/tiingo/daily");
        }
    }

    [Fact]
    public async Task GetDaily_NormalizesSymbol_WhenReplaceDotWithHyphen()
    {
        var handler = new CapturingHandler(OkJson("[]"));
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("tiingo")).Returns(MakeClient(handler));

        var repo = new Mock<IDataProviderRepository>();
        repo.Setup(r => r.GetBaseUrlAsync(DataProvider.Tiingo, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://api.tiingo.com");

        Environment.SetEnvironmentVariable("TIINGO_API_KEY", "x");

        var f = new TiingoFetcher(httpFactory.Object, repo.Object, MakeConfig(replaceDot: true), NullLogger());
        await f.GetDailyAsync("BRK.B", new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2), default);

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Contain("/BRK-B/prices");
    }

    // ------------- helpers -------------
    private sealed class TiingoBarDto
    {
        public DateTime date { get; set; }
        public decimal? open { get; set; }
        public decimal? high { get; set; }
        public decimal? low { get; set; }
        public decimal? close { get; set; }
        public decimal? adjClose { get; set; }
        public long? volume { get; set; }
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _queue = new();
        public int RequestCount { get; private set; }

        public QueueHandler(params HttpResponseMessage[] responses)
        {
            foreach (var r in responses) _queue.Enqueue(r);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_queue.Count > 0 ? _queue.Dequeue()
                                                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }

        public CapturingHandler(HttpResponseMessage response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }
}
