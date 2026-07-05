using Microsoft.UI.Xaml.Data;

namespace PushoverDesktopClient;
public class TitleFallbackConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PushoverMessageEventArgs msg)
        {
            return !string.IsNullOrWhiteSpace(msg.Title) ? msg.Title : msg.Message;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) 
        => throw new NotImplementedException();
}