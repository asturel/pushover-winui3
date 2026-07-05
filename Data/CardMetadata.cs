namespace PushoverDesktopClient;

public class CardMetadata
{
    public DateTime Date { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty; // ÚJ MEZŐ: Ide fűzzük össze a Címet és az Üzenetet!
}
/*
// ==========================================
// 6. APPLICATION ENTRY POINT & BOOTSTRAPPER
// ==========================================
public class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => HandleFatal(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (s, e) => HandleFatal(e.Exception);

        try
        {
            // FIX: Regisztráljuk az eseménykezelőt a legelső lépésben, így a Windows azonnal át tudja lőni az eseményt!
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();

            // Megnézzük, hogy eleve értesítésből indultunk-e
            var activationArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activationArgs.Kind == ExtendedActivationKind.AppNotification)
            {
                // Ha igen, az OnNotificationInvoked aszinkron lefut a háttérben, mi pedig várunk kicsit, majd kilépünk
                Thread.Sleep(1500);
                Environment.Exit(0);
                return;
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) => new App());
        }
        catch (Exception ex)
        {
            HandleFatal(ex);
        }
        finally
        {
            try { AppNotificationManager.Default.Unregister(); } catch { }
        }
    }

    // FIX: Ez a metódus fut le, amikor rákattintasz a Toast értesítésre.
    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            // Kikapjuk az argumentumot, amit feljebb átadtunk (action=viewUrl&url=...)
            string launchArg = args.Argument;

            if (!string.IsNullOrEmpty(launchArg) && launchArg.Contains("url="))
            {
                // Kinyerjük a tiszta URL-t
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

            // Ha nincs URL vagy sima kattintás volt, megpróbáljuk előtérbe hozni a meglévő főablakot
            var activeWindow = PushoverDesktopClient.App.CurrentMainWindow;
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

    public static void HandleFatal(Exception? ex)
    {
        string desc = ex != null ? $"{ex.Message}\n\nTrace:\n{ex.StackTrace}" : "Fatal unmanaged runtime memory violation state mapped.";
        MessageBox(IntPtr.Zero, desc, "WinUI Runtime Guard", 0x00000010);
        Environment.Exit(-1);
    }
}
*/