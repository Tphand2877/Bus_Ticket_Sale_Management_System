using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BusTicketSaleSystem.Models;

namespace BusTicketSaleSystem.Services;

public sealed record WeatherSnapshot(double TemperatureC, string Summary, int WeatherCode)
{
    public string Display => $"{Summary} · {TemperatureC:0.#}°C";
}

public static class ExternalApiServices
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BusTicketSaleSystem/1.0 contact=local-dev");
        return client;
    }

    public static async Task SendTicketEmailAsync(TicketDetails ticket, string recipient, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(AppConfig.BrevoApiKey) || string.IsNullOrWhiteSpace(recipient))
            throw new InvalidOperationException("Thiếu BREVO_API_KEY hoặc email người nhận.");

        var tempFile = Path.Combine(Path.GetTempPath(), $"ticket-{ticket.TicketCode}-{Guid.NewGuid():N}.pdf");
        try
        {
            TicketPrintService.ExportToPdf(TicketPrintModel.FromTicket(ticket), tempFile);
            var bytes = await File.ReadAllBytesAsync(tempFile, cancellationToken);
            var payload = new
            {
                sender = new { name = AppConfig.EmailSenderName, email = AppConfig.EmailSenderAddress },
                to = new[] { new { email = recipient, name = ticket.PassengerName } },
                subject = $"Vé xe {ticket.TicketCode} đã được xác nhận",
                htmlContent = $"<p>Xin chào {System.Net.WebUtility.HtmlEncode(ticket.PassengerName)},</p><p>Vé <strong>{ticket.TicketCode}</strong> của bạn đã được xác nhận.</p><p>Tuyến: {System.Net.WebUtility.HtmlEncode(ticket.Route)}<br/>Khởi hành: {ticket.DepartureTime:dd/MM/yyyy HH:mm}<br/>Ghế: {System.Net.WebUtility.HtmlEncode(ticket.SeatNumber)}</p><p>Vé PDF được đính kèm email này.</p>",
                attachment = new[] { new { content = Convert.ToBase64String(bytes), name = $"Ticket_{ticket.TicketCode}.pdf" } }
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", AppConfig.BrevoApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brevo trả về HTTP {(int)response.StatusCode}.");
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    public static async Task<WeatherSnapshot?> GetWeatherAsync(string place, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(place)) return null;
        var geoUrl = $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&countrycodes=vn&q={Uri.EscapeDataString(place)}";
        using var geo = await Http.GetAsync(geoUrl, cancellationToken);
        if (!geo.IsSuccessStatusCode) return null;
        using var geoDoc = JsonDocument.Parse(await geo.Content.ReadAsStreamAsync(cancellationToken));
        if (geoDoc.RootElement.GetArrayLength() == 0) return null;
        var item = geoDoc.RootElement[0];
        var lat = item.GetProperty("lat").GetDouble();
        var lon = item.GetProperty("lon").GetDouble();
        var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m,weather_code";
        using var weather = await Http.GetAsync(weatherUrl, cancellationToken);
        if (!weather.IsSuccessStatusCode) return null;
        using var weatherDoc = JsonDocument.Parse(await weather.Content.ReadAsStreamAsync(cancellationToken));
        var current = weatherDoc.RootElement.GetProperty("current");
        var temperature = current.GetProperty("temperature_2m").GetDouble();
        var code = current.GetProperty("weather_code").GetInt32();
        return new WeatherSnapshot(temperature, WeatherDescription(code), code);
    }

    public static async Task<IReadOnlyList<string>> GetVietnameseProvincesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync("https://provinces.open-api.vn/api/p/", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return doc.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? "")
            .Where(x => x.Length > 0).OrderBy(x => x, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), false)).ToArray();
    }

    public static void OpenRouteMap(string origin, string destination)
    {
        var url = $"https://www.openstreetmap.org/directions?engine=fossgis_osrm_car&route={Uri.EscapeDataString(origin)}%3B{Uri.EscapeDataString(destination)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string WeatherDescription(int code) => code switch
    {
        0 => "Trời quang", 1 or 2 => "Ít mây", 3 => "Nhiều mây", 45 or 48 => "Sương mù",
        >= 51 and <= 57 => "Mưa phùn", >= 61 and <= 67 => "Mưa", >= 71 and <= 77 => "Tuyết",
        >= 80 and <= 82 => "Mưa rào", >= 95 => "Dông", _ => "Không xác định"
    };
}
