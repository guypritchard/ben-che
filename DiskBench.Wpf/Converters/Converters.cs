using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DiskBench.Wpf;

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent && parameter is string maxWidthStr)
        {
            if (double.TryParse(maxWidthStr, out var maxWidth))
            {
                return Math.Max(0, Math.Min(percent / 100.0 * maxWidth, maxWidth));
            }
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentToScaleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double percent = 0;
        if (value is double d)
            percent = d;
        else if (value is int i)
            percent = i;
        else if (value is float f)
            percent = f;
        
        // Clamp between 0 and 100, then convert to 0.0-1.0 scale
        return Math.Max(0, Math.Min(percent / 100.0, 1.0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentToDashArrayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double percent = 0;
        if (value is double d)
            percent = d;
        else if (value is int i)
            percent = i;
        else if (value is float f)
            percent = f;

        // WPF StrokeDashArray units are multiples of StrokeThickness, not raw pixels.
        // Parameter format: "<circumference>|<strokeThickness>".
        var circumference = 0.0;
        var strokeThickness = 1.0;
        if (parameter != null)
        {
            var paramText = parameter.ToString();
            if (!string.IsNullOrWhiteSpace(paramText))
            {
                var parts = paramText.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out circumference);
                }

                if (parts.Length > 1)
                {
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out strokeThickness);
                }
            }
        }

        if (circumference <= 0 || strokeThickness <= 0)
            return new DoubleCollection { 0, 1 };

        var clamped = Math.Max(0, Math.Min(percent, 100));
        var progress = circumference * (clamped / 100.0) / strokeThickness;
        var remainder = Math.Max(0.0, (circumference / strokeThickness) - progress);

        return new DoubleCollection { progress, remainder };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? 1.0 : 0.5;
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
