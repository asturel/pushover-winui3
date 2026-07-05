using Microsoft.Windows.AppNotifications;

namespace PushoverDesktopClient;

// ==========================================
// 3. TOAST NOTIFICATION HELPER
// ==========================================
public static class NotificationHelper
{
    public static void ShowToast(string title, string message, string appName, string? targetUrl = "", string iconUrl = "", bool silent = false)
    {
        try
        {
            string xmlTitle = System.Security.SecurityElement.Escape(title);
            string xmlMessage = System.Security.SecurityElement.Escape(message);
            string xmlApp = System.Security.SecurityElement.Escape(appName.ToUpper());
            string xmlUrl = System.Security.SecurityElement.Escape(targetUrl ?? "");
            string xmlIcon = System.Security.SecurityElement.Escape(iconUrl);

            // Ha van érvényes ikon URL, generálunk hozzá egy image tag-et, kör alakú vágással
            string imageTag = !string.IsNullOrWhiteSpace(xmlIcon)
                ? $"<image placement='appLogoOverride' hint-crop='circle' src='{xmlIcon}' />"
                : "";

            string audioTag = silent
                    ? "<audio silent='true' />"
                    : "<audio src='ms-winsoundevent:Notification.Default' />";

            string toastXml = $@"
                <toast launch='action=viewUrl&amp;url={xmlUrl}'>
                    {audioTag}                
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{xmlTitle}</text>
                            <text>{xmlMessage}</text>
                            <text attributionText='{xmlApp}' />
                            {imageTag}
                        </binding>
                    </visual>
                </toast>";

            var notification = new AppNotification(toastXml);
            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // Suppress fallback exceptions
        }
    }
}
