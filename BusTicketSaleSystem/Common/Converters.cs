using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BusTicketSaleSystem.Common
{
    public class EqualityToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null && parameter == null) return true;
            if (value == null || parameter == null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

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
}
