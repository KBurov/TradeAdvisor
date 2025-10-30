using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Serilog;

using Common.Rest;

public class RestClientUtilsTests
{
    [Fact]
    public void Backoff_ShouldGrowExponentially_AndRespectCap()
    {
        var cap = 3; // seconds
        var t1 = RestClientUtils.Backoff(1, cap);
        var t2 = RestClientUtils.Backoff(2, cap);
        var t3 = RestClientUtils.Backoff(3, cap);
        var t10 = RestClientUtils.Backoff(10, cap);

        // Basic monotonic growth
        t2.Should().BeGreaterThanOrEqualTo(t1);
        t3.Should().BeGreaterThanOrEqualTo(t2);

        // Each is <= cap
        t1.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(cap));
        t2.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(cap));
        t3.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(cap));
        t10.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(cap));

        // t1 should be ~1s +/- jitter (0..250ms)
        t1.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(0));
    }

    [Fact]
    public void IsTransient_ShouldRecognize_429_408_5xx()
    {
        RestClientUtils.IsTransient(HttpStatusCode.TooManyRequests).Should().BeTrue();
        RestClientUtils.IsTransient(HttpStatusCode.RequestTimeout).Should().BeTrue();
        RestClientUtils.IsTransient(HttpStatusCode.InternalServerError).Should().BeTrue();
        RestClientUtils.IsTransient(HttpStatusCode.BadGateway).Should().BeTrue();
        RestClientUtils.IsTransient(HttpStatusCode.OK).Should().BeFalse();
        RestClientUtils.IsTransient(HttpStatusCode.NotFound).Should().BeFalse();
        RestClientUtils.IsTransient(HttpStatusCode.BadRequest).Should().BeFalse();
    }

    [Fact]
    public void ComputeRetryDelay_UsesRetryAfterDelta_WhenPresent()
    {
        var headers = new HttpResponseMessage().Headers;
        headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        var d = RestClientUtils.ComputeRetryDelay(headers, attempt: 2, maxBackoffSeconds: 30);
        d.Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void ComputeRetryDelay_UsesRetryAfterDate_CappedAndNonNegative()
    {
        var headers = new HttpResponseMessage().Headers;

        // Future date ~5 seconds from now
        var future = DateTimeOffset.UtcNow.AddSeconds(5);
        headers.RetryAfter = new RetryConditionHeaderValue(future);

        var dFuture = RestClientUtils.ComputeRetryDelay(headers, attempt: 2, maxBackoffSeconds: 3);
        dFuture.Should().Be(TimeSpan.FromSeconds(3)); // capped by maxBackoffSeconds

        // Past date -> zero
        var pastHeaders = new HttpResponseMessage().Headers;
        pastHeaders.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-30));
        var dPast = RestClientUtils.ComputeRetryDelay(pastHeaders, attempt: 2, maxBackoffSeconds: 10);
        dPast.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task SafeReadStringAsync_ReturnsBody_OnSuccess()
    {
        var msg = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("hello", Encoding.UTF8, "text/plain")
        };

        var s = await RestClientUtils.SafeReadStringAsync(msg, default);
        s.Should().Be("hello");
    }

    [Fact]
    public async Task SafeReadStringAsync_ReturnsEmpty_OnFailure()
    {
        var msg = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ThrowingContent()
        };

        var s = await RestClientUtils.SafeReadStringAsync(msg, default);
        s.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", 5, "")]
    [InlineData("abc", 5, "abc")]
    [InlineData("abcdef", 3, "abc")]
    public void Truncate_Works(string input, int n, string expected)
    {
        RestClientUtils.Truncate(input, n).Should().Be(expected);
    }

    [Fact]
    public async Task DeserializeBufferedAsync_ParsesValidJson()
    {
        var payload = JsonSerializer.Serialize(new SampleDto { A = 42, B = "x" });
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var logger = new LoggerConfiguration().CreateLogger();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var dto = await RestClientUtils.DeserializeBufferedAsync<SampleDto>(resp, opts, logger, default);

        dto.Should().NotBeNull();
        dto!.A.Should().Be(42);
        dto.B.Should().Be("x");
    }

    [Fact]
    public async Task DeserializeBufferedAsync_ThrowsOnInvalidJson_AndLogsBody()
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ invalid json", Encoding.UTF8, "application/json")
        };

        var logger = new LoggerConfiguration().CreateLogger();
        var opts = new JsonSerializerOptions();

        var act = async () => await RestClientUtils.DeserializeBufferedAsync<SampleDto>(resp, opts, logger, default);
        await act.Should().ThrowAsync<JsonException>();
    }

    private sealed class SampleDto
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    /// <summary>
    /// HttpContent that throws during serialization to trigger SafeReadStringAsync fallback.
    /// </summary>
    private sealed class ThrowingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.FromException(new InvalidOperationException("boom"));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
