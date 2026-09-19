using System.Net;
using System.Net.Http;
using BusTicketSaleSystem.Services;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Behaviour of the loopback VNPay return endpoint. It is unauthenticated by
/// construction and reachable from every local process, so it must reject what the
/// gateway never sends and must never let request data shape a log line.
/// </summary>
public sealed class VnPayListenerTests : IDisposable
{
    private readonly int _port = Random.Shared.Next(24000, 24500);
    private readonly string _logDirectory = Path.Combine(Path.GetTempPath(), $"vnpay-log-{Guid.NewGuid():N}");

    public VnPayListenerTests()
    {
        Environment.SetEnvironmentVariable("VNPAY_CALLBACK_PORT", _port.ToString());
        VnPayCallbackListener.Start();
    }

    public void Dispose()
    {
        VnPayCallbackListener.Stop();
        Environment.SetEnvironmentVariable("VNPAY_CALLBACK_PORT", null);
        if (Directory.Exists(_logDirectory)) Directory.Delete(_logDirectory, recursive: true);
    }

    [SkippableFact]
    public async Task Listener_RejectsNonGetRequests()
    {
        using var client = NewClient();

        using var response = await client.PostAsync($"http://127.0.0.1:{_port}/vnpay-return/?vnp_TxnRef=abc", content: null);

        Skip.If(response.StatusCode == HttpStatusCode.NotFound, "The listener could not bind on this machine.");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [SkippableFact]
    public async Task Listener_RejectsRequestsClaimingAForeignHost()
    {
        using var client = NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_port}/vnpay-return/?vnp_TxnRef=abc");
        request.Headers.Host = "evil.example.com";

        using var response = await client.SendAsync(request);

        Skip.If(response.StatusCode == HttpStatusCode.NotFound, "The listener could not bind on this machine.");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// AUD-D-006: a CRLF in vnp_TxnRef used to be written to the log verbatim, and it
    /// was written before signature verification, so any local caller could forge
    /// audit lines. An unsigned callback must also never be treated as settled.
    /// </summary>
    [SkippableFact]
    public async Task Listener_DoesNotLetRequestDataForgeLogLines()
    {
        using var client = NewClient();
        const string forged = "abc%0D%0A2026-01-01%2000:00:00%20[INF]%20VNPay%20return%20settled";

        using var response = await client.GetAsync($"http://127.0.0.1:{_port}/vnpay-return/?vnp_TxnRef={forged}");

        Skip.If(response.StatusCode == HttpStatusCode.NotFound, "The listener could not bind on this machine.");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not completed", body, StringComparison.OrdinalIgnoreCase);

        var sanitized = InvokeSanitize("abc\r\n2026-01-01 [INF] forged");
        Assert.DoesNotContain('\r', sanitized);
        Assert.DoesNotContain('\n', sanitized);
    }

    private static string InvokeSanitize(string value)
    {
        var method = typeof(VnPayCallbackListener)
            .GetMethod("SanitizeForLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [value])!;
    }

    private static HttpClient NewClient() => new() { Timeout = TimeSpan.FromSeconds(10) };
}
