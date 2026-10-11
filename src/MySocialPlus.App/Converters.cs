using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MySocialPlus.App;

/// <summary>
/// A picture address → a picture, downloaded in the background and kept small. A bad address
/// shows nothing rather than an error.
/// </summary>
public sealed class ImageUrlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.DecodePixelWidth = parameter is string s && int.TryParse(s, out var w) ? w : 640;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }
        catch (Exception e) when (e is UriFormatException or NotSupportedException or System.IO.IOException) { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class BoolToVisibility : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible when the value is a non-empty string or any other non-null value.</summary>
public sealed class NullToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Times the way a person says them: "just now", "5 min ago", "yesterday 3:40 PM", "Oct 3".</summary>
public static class When
{
    public static string Ago(DateTimeOffset t, DateTimeOffset now)
    {
        var local = t.ToLocalTime();
        var d = now - t;
        if (d < TimeSpan.FromMinutes(1)) return "just now";
        if (d < TimeSpan.FromHours(1)) return $"{(int)d.TotalMinutes} min ago";
        if (d < TimeSpan.FromHours(12)) return $"{(int)d.TotalHours} h ago";
        var today = now.ToLocalTime().Date;
        if (local.Date == today) return "today " + local.ToString("t", CultureInfo.CurrentCulture);
        if (local.Date == today.AddDays(-1)) return "yesterday " + local.ToString("t", CultureInfo.CurrentCulture);
        if (local.Year == now.ToLocalTime().Year) return local.ToString("MMM d", CultureInfo.CurrentCulture);
        return local.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
    }

    public static string Full(DateTimeOffset t) => t.ToLocalTime().ToString("dddd, MMMM d, yyyy h:mm tt", CultureInfo.CurrentCulture);
}
