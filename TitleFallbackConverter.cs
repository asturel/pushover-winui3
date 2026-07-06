using System;
using Microsoft.UI.Xaml.Data;

namespace PushoverDesktopClient;

public class TitleFallbackConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Expecting value to be either Title or a complex object; fallback to a short message if title missing
        if (value is string s && !string.IsNullOrWhiteSpace(s)) return s;
        return "(no title)";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
