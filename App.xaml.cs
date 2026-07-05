using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace PushoverDesktopClient;

public delegate void ConfigChangedEventHandler();

public partial class App : Application
{
    public static MainWindow? CurrentMainWindow { get; private set; }

    public static IConfigService Config { get; } = new ConfigService();

    // Esemény, ami értesíti a MainWindow-t, ha a beállítások mentésre kerültek
    public static event ConfigChangedEventHandler? ConfigChanged;

    public static void NotifyConfigChanged() => ConfigChanged?.Invoke();


    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;

    public App()
    {
        this.InitializeComponent();

        // Register with Windows Toast Notification platform and wire up click handler
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        AppNotificationManager.Default.Register();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            CurrentMainWindow = new MainWindow();
            CurrentMainWindow.Activate();

            CurrentMainWindow.Closed += (sender, e) =>
            {
                try
                {
                    AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                    AppNotificationManager.Default.Unregister();
                }
                catch
                {
                    // Suppress cleanup exceptions
                }
            };

            try
            {
                CurrentMainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));
            }
            catch { }
        }
        catch (Exception ex)
        {
            string errorMessage = $"Application failed to start.\n\nException: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\n\nInner Exception: {ex.InnerException.Message}";
            }
            MessageBox(IntPtr.Zero, errorMessage, "Pushover Client - Critical Error", MB_OK | MB_ICONERROR);
            Environment.Exit(1);
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            string launchArg = args.Argument;

            // Extract hyperlink payload if standard routing parameter exists
            if (!string.IsNullOrEmpty(launchArg) && launchArg.Contains("url="))
            {
                string targetUrl = launchArg.Substring(launchArg.IndexOf("url=") + 4);

                if (!string.IsNullOrWhiteSpace(targetUrl) && Uri.IsWellFormedUriString(targetUrl, UriKind.Absolute))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetUrl,
                        UseShellExecute = true
                    });
                    return;
                }
            }

            // Standard fallback fallback: Bring existing window instance back to focus
            var activeWindow = CurrentMainWindow;
            if (activeWindow != null)
            {
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(activeWindow);
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                }
            }
        }
        catch { }
    }
}