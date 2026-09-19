using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using BusTicketSaleSystem.Models;

namespace BusTicketSaleSystem.Services;

/// <summary>
/// Thrown when an outbound third-party call cannot be completed. The message is safe to
/// show to the user; the inner exception (when present) carries the transport detail.
/// </summary>
public sealed class ExternalServiceException : Exception
{
    public ExternalServiceException(string message) : base(message) { }
    public ExternalServiceException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Outbound integrations. Every member throws <see cref="ExternalServiceException"/> with an
/// English, user-presentable message when the operation cannot be completed.
/// </summary>
public static class ExternalApiServices
{
    private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BusTicketSaleSystem/1.0 (+https://omnibus.local; desktop-client)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    /// <summary>True when an e-mail can actually be sent (transactional API key and sender configured).</summary>
    public static bool IsEmailConfigured =>
        !string.IsNullOrWhiteSpace(AppConfig.BrevoApiKey) && !string.IsNullOrWhiteSpace(AppConfig.EmailSenderAddress);

    /// <summary>
    /// Renders <paramref name="ticket"/> to a PDF and e-mails it to <paramref name="recipient"/>
    /// through the Brevo transactional API.
    /// </summary>
    public static async Task SendTicketEmailAsync(TicketDetails ticket, string recipient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (string.IsNullOrWhiteSpace(AppConfig.BrevoApiKey))
            throw new ExternalServiceException("E-mail delivery is not configured: the BREVO_API_KEY environment variable is missing.");
        if (string.IsNullOrWhiteSpace(AppConfig.EmailSenderAddress))
            throw new ExternalServiceException("E-mail delivery is not configured: the EMAIL_SENDER_ADDRESS environment variable is missing.");
        if (string.IsNullOrWhiteSpace(recipient))
            throw new ExternalServiceException("No recipient e-mail address is available for this account.");

        var tempFile = Path.Combine(Path.GetTempPath(), $"ticket-{ticket.TicketCode}-{Guid.NewGuid():N}.pdf");
        try
        {
            byte[] pdfBytes;
            try
            {
                TicketPrintService.ExportToPdf(TicketPrintModel.FromTicket(ticket), tempFile);
                pdfBytes = await File.ReadAllBytesAsync(tempFile, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ExternalServiceException("The ticket PDF could not be generated for e-mail delivery.", ex);
            }

            var passengerName = string.IsNullOrWhiteSpace(ticket.PassengerName) ? "Quý khách" : ticket.PassengerName;
            var payload = new
            {
                sender = new { name = AppConfig.EmailSenderName, email = AppConfig.EmailSenderAddress },
                to = new[] { new { email = recipient, name = passengerName } },
                subject = $"[OmniBus Việt Nam] Vé xe điện tử xác nhận: {ticket.TicketCode} ({ticket.Route})",
                htmlContent = BuildTicketEmailHtml(ticket, passengerName),
                attachment = new[] { new { content = Convert.ToBase64String(pdfBytes), name = $"Ve_Xe_{ticket.TicketCode}.pdf" } }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint);
            request.Headers.Add("api-key", AppConfig.BrevoApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await Http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new ExternalServiceException("The e-mail service could not be reached. Please check your internet connection.", ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ExternalServiceException("The e-mail service did not respond in time. Please try again.", ex);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode) return;

                var detail = await ReadErrorMessageAsync(response, cancellationToken);
                throw new ExternalServiceException(
                    $"The e-mail service rejected the request (HTTP {(int)response.StatusCode}){detail}.");
            }
        }
        finally
        {
            TryDeleteTempFile(tempFile);
        }
    }

    /// <summary>Opens the default browser on a driving route between the two stops.</summary>
    /// <summary>
    /// Mở Google Maps điều hướng lộ trình giữa 2 điểm xuất phát và điểm đến tại Việt Nam.
    /// </summary>
    public static void OpenRouteMap(string origin, string destination)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
            throw new ExternalServiceException("Vé không có đầy đủ điểm đi và điểm đến để mở bản đồ lộ trình.");

        var url = "https://www.google.com/maps/dir/?api=1"
                  + $"&origin={Uri.EscapeDataString(origin.Trim() + ", Việt Nam")}"
                  + $"&destination={Uri.EscapeDataString(destination.Trim() + ", Việt Nam")}"
                  + "&travelmode=driving&hl=vi";
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            throw new ExternalServiceException("Không thể mở trình duyệt để hiển thị bản đồ chỉ đường Google Maps.", ex);
        }
    }

    private static string BuildTicketEmailHtml(TicketDetails ticket, string passengerName)
    {
        var name = WebUtility.HtmlEncode(passengerName);
        var route = WebUtility.HtmlEncode(ticket.Route);
        var seats = WebUtility.HtmlEncode(ticket.SeatNumbers);
        var code = WebUtility.HtmlEncode(ticket.TicketCode);
        var price = ticket.Price.ToString("N0") + " VND";
        return $"<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #1E293B;'>"
             + $"<h2 style='color: #0F4C81;'>XÁC NHẬN VÉ XE ĐIỆN TỬ - OMNIBUS VIỆT NAM</h2>"
             + $"<p>Kính gửi <strong>{name}</strong>,</p>"
             + $"<p>Vé xe của quý khách với mã đặt chỗ <strong>{code}</strong> đã được xác nhận thành công trên hệ thống bến xe.</p>"
             + $"<table style='width: 100%; max-width: 500px; border-collapse: collapse; margin: 16px 0;'>"
             + $"  <tr><td style='padding: 6px 0; color: #64748B;'>Tuyến đường:</td><td style='font-weight: bold;'>{route}</td></tr>"
             + $"  <tr><td style='padding: 6px 0; color: #64748B;'>Giờ xuất bến:</td><td style='font-weight: bold;'>{ticket.DepartureTime:dd/MM/yyyy HH:mm}</td></tr>"
             + $"  <tr><td style='padding: 6px 0; color: #64748B;'>Số ghế:</td><td style='font-weight: bold;'>{seats}</td></tr>"
             + $"  <tr><td style='padding: 6px 0; color: #64748B;'>Tổng tiền:</td><td style='font-weight: bold; color: #0F4C81;'>{price}</td></tr>"
             + $"</table>"
             + $"<p><em>File PDF vé xe điện tử (kèm mã QR soát vé tại bến) đã được đính kèm trong email này.</em></p>"
             + $"<p>Quý khách vui lòng có mặt tại bến trước giờ xe chạy 15-30 phút để xuất trình vé và lên xe.</p>"
             + $"<hr style='border: none; border-top: 1px solid #E2E8F0; margin: 20px 0;'/>"
             + $"<p style='font-size: 12px; color: #94A3B8;'>Hệ thống quản lý bán vé xe khách liên tỉnh Omnibus Việt Nam · Hotline hỗ trợ: 1900 1234</p>"
             + $"</div>";
    }

    /// <summary>
    /// Extracts Brevo's error message defensively — the body may be empty, HTML, or a JSON
    /// object without the documented fields.
    /// </summary>
    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            LoggerService.LogWarning($"Could not read the e-mail service error body: {ex.Message}");
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String &&
                message.GetString() is { Length: > 0 } text)
            {
                return $": {text}";
            }
        }
        catch (JsonException)
        {
            LoggerService.LogWarning("The e-mail service returned a non-JSON error body.");
        }

        return string.Empty;
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoggerService.LogWarning($"Could not delete the temporary ticket PDF '{path}': {ex.Message}");
        }
    }
}
