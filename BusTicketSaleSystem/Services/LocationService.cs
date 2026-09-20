using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;

namespace BusTicketSaleSystem.Services;

/// <summary>
/// Quản lý vị trí thiết bị và phạm vi bến xe hoạt động tại Việt Nam.
/// - Với Admin: Vị trí được cố định theo máy trạm (mặc định Bến xe Thái Nguyên),
///   chỉ được phép quản trị các chuyến/tuyến thuộc phạm vi bến xe này.
/// - Với User/Khách hàng: Vị trí mặc định lấy theo vị trí máy, nhưng có thể
///   tùy chọn thay đổi để tìm và đặt vé từ bất kỳ tỉnh thành nào trên cả nước.
/// </summary>
public sealed class LocationService
{
    private static readonly Lazy<LocationService> LazyInstance = new(() => new LocationService());
    public static LocationService Instance => LazyInstance.Value;

    public const string DefaultStation = "Thái Nguyên";

    private string _machineLocation = DefaultStation;
    private string _currentLocation = DefaultStation;
    private bool _isDetecting;

    public event EventHandler? LocationChanged;

    /// <summary>
    /// Vị trí vật lý cố định của máy trạm (Bến xe sở tại).
    /// </summary>
    public string MachineLocation
    {
        get => _machineLocation;
        private set
        {
            _machineLocation = value;
            if (CurrentUser.IsAdmin)
            {
                _currentLocation = value;
                LocationChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Vị trí làm việc hiện tại.
    /// Với Admin: Luôn bị khóa chặt theo MachineLocation, không thể đổi sang bến khác.
    /// Với User: Có thể thay đổi linh hoạt để đặt vé từ mọi nơi.
    /// </summary>
    public string CurrentLocation
    {
        get => CurrentUser.IsAdmin ? MachineLocation : _currentLocation;
        set => SetUserLocation(value);
    }

    /// <summary>
    /// Danh sách 63 tỉnh thành phố và bến xe trên toàn quốc tại Việt Nam.
    /// </summary>
    public IReadOnlyList<string> AvailableLocations => VietnamProvinces.List;

    /// <summary>
    /// True when the station was pinned by configuration rather than guessed from the
    /// network. A pinned station is authoritative and geolocation may not override it.
    /// </summary>
    public bool IsStationPinned { get; }

    private LocationService()
    {
        // 1. Kiểm tra cấu hình môi trường hoặc file config
        var envStation = Environment.GetEnvironmentVariable("STATION_LOCATION");
        if (!string.IsNullOrWhiteSpace(envStation))
        {
            var normalized = NormalizeToVietnamProvince(envStation);
            _machineLocation = normalized;
            _currentLocation = normalized;
            IsStationPinned = true;
        }

        // 2. Kích hoạt dò vị trí máy qua IP Geolocation nền (non-blocking)
        _ = DetectMachineLocationAsync();
    }

    /// <summary>
    /// Thay đổi vị trí của tài khoản người dùng.
    /// Bị từ chối nếu tài khoản đang là Admin (Admin bị khóa theo máy trạm).
    /// </summary>
    public bool SetUserLocation(string? newLocation)
    {
        if (string.IsNullOrWhiteSpace(newLocation)) return false;

        var normalized = NormalizeToVietnamProvince(newLocation);
        if (CurrentUser.IsAdmin)
        {
            // Admin không thể tự đổi sang bến xe khác
            LoggerService.LogWarning($"Tài khoản Admin đã thử đổi vị trí từ {MachineLocation} sang {normalized}, thao tác bị từ chối theo chính sách bảo mật bến xe.");
            return false;
        }

        if (!string.Equals(_currentLocation, normalized, StringComparison.OrdinalIgnoreCase))
        {
            _currentLocation = normalized;
            LocationChanged?.Invoke(this, EventArgs.Empty);
            LoggerService.LogInfo($"Người dùng đã chuyển đổi vị trí đặt vé sang: {normalized}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Thiết lập vị trí máy cố định (dùng cho cấu hình hệ thống hoặc kiểm thử).
    /// </summary>
    public void SetFixedMachineLocation(string location)
    {
        var normalized = NormalizeToVietnamProvince(location);
        _machineLocation = normalized;
        _currentLocation = normalized;
        LocationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Dò tìm vị trí IP của máy tại Việt Nam qua API Geolocation nhanh và chuẩn xác.
    /// Tối ưu riêng cho phạm vi lãnh thổ Việt Nam.
    /// </summary>
    public async Task DetectMachineLocationAsync()
    {
        if (_isDetecting) return;
        _isDetecting = true;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OmniBus-Transit-VN/1.0");

            // HTTPS: the reply used to travel in cleartext, so anything on the path
            // could choose the station an administrator was scoped to (AUD-D-008).
            var response = await client.GetStringAsync("https://ip-api.com/json/?fields=status,country,regionName,city");
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
            {
                var city = root.TryGetProperty("city", out var c) ? c.GetString() : null;
                var region = root.TryGetProperty("regionName", out var r) ? r.GetString() : null;

                var detected = city ?? region;
                if (!string.IsNullOrWhiteSpace(detected))
                {
                    var normalized = NormalizeToVietnamProvince(detected);

                    // Geolocation is a convenience hint for customers only. It must
                    // never decide which station an administrator may operate on:
                    // that value comes from STATION_LOCATION (AUD-D-008).
                    if (!IsStationPinned) MachineLocation = normalized;
                    if (!CurrentUser.IsAdmin && _currentLocation == DefaultStation)
                    {
                        _currentLocation = normalized;
                    }
                    LoggerService.LogInfo($"Dò vị trí máy trạm thành công qua IP Việt Nam: {detected} -> {normalized}");
                }
            }
        }
        catch (Exception ex)
        {
            // Nếu mất mạng hoặc dùng offline, giữ nguyên bến xe mặc định của máy
            LoggerService.LogInfo($"Dò vị trí IP trực tuyến không thành công, sử dụng bến xe mặc định của máy ({MachineLocation}): {ex.Message}");
        }
        finally
        {
            _isDetecting = false;
        }
    }

    /// <summary>
    /// Chuẩn hóa tên tỉnh thành / bến xe sang danh mục 63 tỉnh thành Việt Nam chuẩn.
    /// </summary>
    public static string NormalizeToVietnamProvince(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultStation;

        var input = raw.Trim();

        // Loại bỏ tiền tố "Bến xe", "Tỉnh", "Thành phố", "TP."
        var clean = input;
        foreach (var prefix in new[] { "Bến xe", "bến xe", "Tỉnh", "tỉnh", "Thành phố", "thành phố", "TP.", "TP " })
        {
            if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(prefix.Length).Trim();
            }
        }

        // Khớp trực tiếp với danh mục tỉnh thành Việt Nam
        var exact = VietnamProvinces.List.FirstOrDefault(p =>
            string.Equals(p, clean, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p, input, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Khớp mờ theo không dấu hoặc ký tự chứa
        var contains = VietnamProvinces.List.FirstOrDefault(p =>
            clean.Contains(p, StringComparison.OrdinalIgnoreCase) ||
            p.Contains(clean, StringComparison.OrdinalIgnoreCase));
        if (contains != null) return contains;

        // Xử lý các tên tiếng Anh / không dấu phổ biến trả về từ IP Geolocation
        return clean.ToLowerInvariant() switch
        {
            "thai nguyen" or "thainguyen" => "Thái Nguyên",
            "hanoi" or "ha noi" => "Hà Nội",
            "ho chi minh" or "ho chi minh city" or "saigon" or "sai gon" => "TP. Hồ Chí Minh",
            "da nang" or "danang" => "Đà Nẵng",
            "hai phong" or "haiphong" => "Hải Phòng",
            "can tho" or "cantho" => "Cần Thơ",
            "quang ninh" or "ha long" => "Quảng Ninh",
            "bac kan" or "backan" => "Bắc Kạn",
            "bac ninh" or "bacninh" => "Bắc Ninh",
            "bac giang" or "bacgiang" => "Bắc Giang",
            "nam dinh" or "namdinh" => "Nam Định",
            "ninh binh" or "ninhbinh" => "Ninh Bình",
            "thanh hoa" or "thanhhoa" => "Thanh Hóa",
            "nghe an" or "vinh" => "Nghệ An",
            "ha tinh" or "hatinh" => "Hà Tĩnh",
            "lao cai" or "sapa" or "sa pa" => "Lào Cai",
            "phu tho" or "viet tri" => "Phú Thọ",
            _ => clean.Length > 0 ? clean : DefaultStation
        };
    }
}
