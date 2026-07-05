using System;
using Microsoft.UI.Xaml.Data;

namespace PushoverDesktopClient;

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.TotalDays >= 365) return $"{(int)(span.TotalDays / 365)}y ago";
            if (span.TotalDays >= 30) return $"{(int)(span.TotalDays / 30)}mo ago";
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d ago";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h ago";
            if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m ago";
            return "just now";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}