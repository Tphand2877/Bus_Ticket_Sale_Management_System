using System.Net;
using System.Text;

namespace BusTicketSaleSystem.Services;

/// <summary>
/// Receives the VNPay browser return directly inside the desktop process. The gateway redirects to
/// <c>http://127.0.0.1:&lt;port&gt;/vnpay-return/</c>; the query string is verified and settled by
/// <see cref="PaymentService.VerifyVnPayCallbackAsync"/> and the outcome is published to any open
/// payment window through <see cref="PaymentSettled"/>.
/// </summary>
public static class VnPayCallbackListener
{
    private static readonly object Gate = new();
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cancellation;
    private static Task? _loop;

    /// <summary>Raised with the gateway transaction reference and whether the payment settled successfully.</summary>
    public static event Action<string, bool>? PaymentSettled;

    /// <summary>True when the loopback listener is bound and accepting gateway returns.</summary>
    public static bool IsListening
    {
        get { lock (Gate) return _listener is not null; }
    }

    public static void Start()
    {
        lock (Gate)
        {
            if (_listener is not null) return;

            var prefix = AppConfig.VnPayCallbackPrefix;
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                listener.Close();
                LoggerService.LogWarning(
                    $"VNPay return listener could not bind {prefix} ({ex.Message}). " +
                    "Gateway payments will fall back to status polling inside the payment window.");
                return;
            }
            catch (ObjectDisposedException ex)
            {
                LoggerService.LogWarning($"VNPay return listener could not start on {prefix}: {ex.Message}");
                return;
            }

            var cancellation = new CancellationTokenSource();
            _listener = listener;
            _cancellation = cancellation;
            _loop = Task.Run(() => ListenAsync(listener, cancellation.Token));
            LoggerService.LogInfo($"VNPay return listener is accepting callbacks on {prefix}.");
        }
    }

    public static void Stop()
    {
        HttpListener? listener;
        CancellationTokenSource? cancellation;
        Task? loop;
        lock (Gate)
        {
            if (_listener is null) return;
            listener = _listener;
            cancellation = _cancellation;
            loop = _loop;
            _listener = null;
            _cancellation = null;
            _loop = null;
        }

        cancellation?.Cancel();
        try { listener.Stop(); }
        catch (ObjectDisposedException ex) { LoggerService.LogWarning($"VNPay return listener was already disposed: {ex.Message}"); }
        listener.Close();
        try { loop?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException ex) { LoggerService.LogWarning($"VNPay return listener loop ended with: {ex.GetBaseException().Message}"); }
        cancellation?.Dispose();
        LoggerService.LogInfo("VNPay return listener stopped.");
    }

    private static async Task ListenAsync(HttpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException ex)
            {
                if (!token.IsCancellationRequested)
                    LoggerService.LogWarning($"VNPay return listener stopped accepting requests: {ex.Message}");
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException ex)
            {
                LoggerService.LogWarning($"VNPay return listener is no longer running: {ex.Message}");
                return;
            }

            try
            {
                await HandleAsync(context);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("VNPay return callback could not be handled.", ex);
                TryWrite(context, "Payment status unavailable",
                    "We could not read the payment result. Please check your booking in the app.");
            }
        }
    }

    private static async Task HandleAsync(HttpListenerContext context)
    {
        var query = ParseQuery(context.Request.Url?.Query);
        var transactionNo = query.GetValueOrDefault("vnp_TxnRef", string.Empty);
        LoggerService.LogInfo($"VNPay return received for {(transactionNo.Length == 0 ? "an unknown reference" : transactionNo)} " +
                              $"with {query.Count} field(s).");

        if (query.Count == 0)
        {
            TryWrite(context, "Nothing to verify", "This address only accepts VNPay payment returns.");
            return;
        }

        var verification = await PaymentService.VerifyVnPayCallbackAsync(query);
        if (verification.IsSuccess)
        {
            TryWrite(context, "Payment successful",
                verification.AlreadyProcessed
                    ? "This payment was already confirmed. You can close this window and return to the app."
                    : "Your ticket is now paid. You can close this window and return to the app.");
        }
        else
        {
            TryWrite(context, "Payment not completed",
                $"{verification.ErrorMessage ?? "The payment was not completed."} You can close this window and return to the app.");
        }

        LoggerService.LogInfo($"VNPay return for {transactionNo} settled: success={verification.IsSuccess}, alreadyProcessed={verification.AlreadyProcessed}.");

        if (transactionNo.Length == 0) return;
        try
        {
            PaymentSettled?.Invoke(transactionNo, verification.IsSuccess);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"A VNPay settlement subscriber failed for {transactionNo}.", ex);
        }
    }

    private static void TryWrite(HttpListenerContext context, string heading, string message)
    {
        try
        {
            var html = $"""
                <!DOCTYPE html>
                <html lang="en"><head><meta charset="utf-8"><title>{WebUtility.HtmlEncode(heading)}</title></head>
                <body style="font-family:Segoe UI,Arial,sans-serif;background:#f8fafc;color:#0f172a;padding:48px;text-align:center">
                <h1 style="font-size:22px">{WebUtility.HtmlEncode(heading)}</h1>
                <p style="font-size:15px;color:#475569">{WebUtility.HtmlEncode(message)}</p>
                </body></html>
                """;
            var payload = Encoding.UTF8.GetBytes(html);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = payload.LongLength;
            context.Response.OutputStream.Write(payload, 0, payload.Length);
            context.Response.Close();
        }
        catch (HttpListenerException ex)
        {
            LoggerService.LogWarning($"Could not write the VNPay return page: {ex.Message}");
        }
        catch (ObjectDisposedException ex)
        {
            LoggerService.LogWarning($"Could not write the VNPay return page: {ex.Message}");
        }
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query)) return fields;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) continue;
            var key = WebUtility.UrlDecode(pair[..separator]);
            if (string.IsNullOrEmpty(key)) continue;
            fields[key] = WebUtility.UrlDecode(pair[(separator + 1)..]);
        }
        return fields;
    }
}
