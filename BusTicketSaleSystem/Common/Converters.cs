using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BusTicketSaleSystem.Models;

namespace BusTicketSaleSystem.Common
{
    /// <summary>
    /// Maps the CHECK-constrained Vietnamese <c>Tickets.Status</c> values to the English UI labels.
    /// </summary>
    public class TicketStatusToDisplayConverter : IValueConverter
    {
        public static string ToDisplay(string? status) => status?.Trim() switch
        {
            TicketStatuses.Reserved => "Reserved",
            TicketStatuses.Paid => "Paid",
            TicketStatuses.Cancelled => "Cancelled",
            TicketStatuses.Used => "Used",
            null or "" => string.Empty,
            var other => other
        };

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => ToDisplay(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Maps both <c>Tickets.PaymentStatus</c> (Vietnamese) and <c>Payments.Status</c> (ledger)
    /// values to the English UI labels.
    /// </summary>
    public class PaymentStatusToDisplayConverter : IValueConverter
    {
        public static string ToDisplay(string? status) => status?.Trim() switch
        {
            TicketStatuses.Unpaid => "Unpaid",
            TicketStatuses.Paid => "Paid",
            TicketStatuses.Refunded => "Refunded",
            "Pending" => "Pending",
            "Success" => "Settled",
            "Failed" => "Failed",
            "Refunded" => "Refunded",
            null or "" => string.Empty,
            var other => other
        };

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => ToDisplay(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class TabActiveBrushConverter : IValueConverter
    {
        public Brush ActiveBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
        public Brush InactiveBrush { get; set; } = Brushes.Transparent;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return InactiveBrush;
            bool isActive = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            return isActive ? ActiveBrush : InactiveBrush;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class TabActiveTextBrushConverter : IValueConverter
    {
        public Brush ActiveBrush { get; set; } = Brushes.White;
        public Brush InactiveBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return InactiveBrush;
            bool isActive = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            return isActive ? ActiveBrush : InactiveBrush;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class BooleanToVisibilityInvertedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                return v != Visibility.Visible;
            }
            return false;
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }

    /// <summary>
    /// Semantic palette for one ticket/payment state: surface, border and text that agree with
    /// each other. Hard-coding these in XAML made every ticket render success-green, so a
    /// <c>Đã hủy</c> ticket showed a green tick.
    /// </summary>
    public readonly record struct StatusPalette(string Surface, string Border, string Accent)
    {
        public static StatusPalette ForTicketStatus(string? status) => status?.Trim() switch
        {
            TicketStatuses.Paid => Success,
            TicketStatuses.Used => Neutral,
            TicketStatuses.Cancelled => Danger,
            TicketStatuses.Reserved => Warning,
            TicketStatuses.Refunded => Info,
            TicketStatuses.Unpaid => Warning,
            _ => Info
        };

        public static readonly StatusPalette Success = new("#ECFDF5", "#A7F3D0", "#065F46");
        public static readonly StatusPalette Warning = new("#FFFBEB", "#FDE68A", "#92400E");
        public static readonly StatusPalette Danger = new("#FEF2F2", "#FECACA", "#991B1B");
        public static readonly StatusPalette Info = new("#EFF6FF", "#BFDBFE", "#1E40AF");
        public static readonly StatusPalette Neutral = new("#F1F5F9", "#E2E8F0", "#475569");
    }

    /// <summary>
    /// Which slice of the palette a bound element wants.
    /// </summary>
    public enum StatusBrushRole
    {
        Surface,
        Border,
        Accent
    }

    /// <summary>
    /// Maps a ticket status to a brush. <c>ConverterParameter</c> selects the role:
    /// <c>Surface</c> (default), <c>Border</c> or <c>Accent</c>.
    /// </summary>
    public class TicketStatusToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var palette = StatusPalette.ForTicketStatus(value as string);
            var role = ParseRole(parameter);
            var hex = role switch
            {
                StatusBrushRole.Border => palette.Border,
                StatusBrushRole.Accent => palette.Accent,
                _ => palette.Surface
            };
            return Brush(hex);
        }

        private static StatusBrushRole ParseRole(object? parameter) =>
            Enum.TryParse(parameter as string, ignoreCase: true, out StatusBrushRole role)
                ? role
                : StatusBrushRole.Surface;

        internal static SolidColorBrush Brush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            // Frozen: these are handed to many elements and must never be mutated in place.
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Maps a <see cref="Services.NotificationKind"/> to a brush, same role parameter as
    /// <see cref="TicketStatusToBrushConverter"/>. Drives the shell toast.
    /// </summary>
    public class NotificationKindToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var palette = value switch
            {
                Services.NotificationKind.Success => StatusPalette.Success,
                Services.NotificationKind.Warning => StatusPalette.Warning,
                Services.NotificationKind.Error => StatusPalette.Danger,
                _ => StatusPalette.Info
            };

            var hex = (parameter as string)?.ToLowerInvariant() switch
            {
                "border" => palette.Border,
                "accent" => palette.Accent,
                _ => palette.Surface
            };
            return TicketStatusToBrushConverter.Brush(hex);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Glyph for a notification kind, so the toast is distinguishable without relying on colour
    /// alone (colour-blind users, and colour is not announced by screen readers).
    /// </summary>
    public class NotificationKindToGlyphConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
        {
            Services.NotificationKind.Success => "✓",
            Services.NotificationKind.Warning => "!",
            Services.NotificationKind.Error => "✕",
            _ => "i"
        };

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
