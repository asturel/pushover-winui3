using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PushoverDesktopClient;

public sealed partial class MainWindow : Window
{
    public ObservableCollection<PushoverMessageEventArgs> DisplayedMessages { get; } = new();

    private readonly List<PushoverMessageEventArgs> _allMessages = new();
    private readonly HashSet<string> _discoveredApps = new();
    private readonly HashSet<long> _processedMessageIds = new();
    private readonly object _lockObj = new();

    private readonly IMessageStorage _storage;
    private PushoverWebSocketService? _wsService;
    private CancellationTokenSource? _cts;

    private readonly DispatcherTimer _timeRefreshTimer;

    private string _currentAppFilter = "All";
    private string _currentSearchQuery = string.Empty;

    private readonly string _configFilePath;
    private readonly string _cacheFilePath;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

    public MainWindow()
    {
        this.InitializeComponent();
        _storage = new SqliteMessageStorage();

        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PushoverDesktopClient");
        Directory.CreateDirectory(appDataFolder);
        _cacheFilePath = Path.Combine(appDataFolder, "deviceid_cache.json");

        AddLogLine($"[UI] Target config path: {_configFilePath}");
        AddLogLine($"[UI] Secure token storage path: {_cacheFilePath}");

        // Subscribe to settings window save event
        App.ConfigChanged += OnConfigChanged;

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            var messages = _storage.LoadAllMessages();
            foreach (var m in messages)
            {
                AddMessageCard(m, isHistory: true);
            }
        });
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            StartClient_Click(null!, null!);
        });

        SetWindowIcon();

        // Start timer to refresh the "X minutes ago" labels every minute
        _timeRefreshTimer = new DispatcherTimer();
        _timeRefreshTimer.Interval = TimeSpan.FromMinutes(1);
        _timeRefreshTimer.Tick += (s, e) =>
        {
            if (App.Config.Current.UseRelativeTime)
            {
                RefreshVisibleTimestamps();
            }
            RemoveExpiredMessages();
        };
        _timeRefreshTimer.Start();
    }

    private void OnConfigChanged()
    {
        ApplyFiltering();
        RefreshVisibleTimestamps();
    }

    private void SetWindowIcon()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        const int ICON_SMALL = 0;
        const int ICON_BIG = 1;
        const uint WM_SETICON = 0x0080;

        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            IntPtr hIcon = LoadImage(IntPtr.Zero, iconPath, 1, 32, 32, 0x00000010);
            if (hIcon != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, ICON_SMALL, hIcon);
                SendMessage(hwnd, WM_SETICON, ICON_BIG, hIcon);
            }
        }
    }

    public void AddMessageCard(PushoverMessageEventArgs msg, bool isHistory = false)
    {
        if (msg.Id != 0)
        {
            lock (_lockObj)
            {
                if (_processedMessageIds.Contains(msg.Id)) return;
                _processedMessageIds.Add(msg.Id);
            }
        }
        else
        {
            AddLogLine($"[UI] Warning: Received a message with ID=0 ({msg.Title}). This message will not be stored in the processed list.");
        }

        if (msg.IsRealTime && !isHistory)
        {
            NotificationHelper.ShowToast(msg.Title ?? msg.Message.Substring(0, Math.Min(100, msg.Message.Length)), msg.Message, msg.Application, msg.Url, msg.IconUrl, msg.Priority <= 0);
        }

        this.DispatcherQueue.TryEnqueue(() =>
        {
            // Dynamic sidebar filter setup
            if (!_discoveredApps.Contains(msg.Application))
            {
                _discoveredApps.Add(msg.Application);

                var btn = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Padding = new Thickness(10)
                };

                var btnContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                var appIconImg = new Image { Width = 20, Height = 20 };
                string appIconImgUrl = msg.IconUrl;
                appIconImg.Source = new BitmapImage(new Uri(appIconImgUrl));

                btnContent.Children.Add(appIconImg);
                btnContent.Children.Add(new TextBlock { Text = msg.Application });
                btn.Content = btnContent;

                btn.Click += (s, e) =>
                {
                    _currentAppFilter = msg.Application;
                    ApplyFiltering(true);
                };

                int insertIndex = 1;
                while (insertIndex < SidebarPanel.Children.Count)
                {
                    if (SidebarPanel.Children[insertIndex] is Button existingBtn &&
                        existingBtn.Content is StackPanel existingStack &&
                        existingStack.Children.Count > 1 &&
                        existingStack.Children[1] is TextBlock existingTxt)
                    {
                        if (string.Compare(msg.Application, existingTxt.Text, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            break;
                        }
                    }
                    insertIndex++;
                }
                SidebarPanel.Children.Insert(insertIndex, btn);
            }

            lock (_lockObj)
            {
                _allMessages.Add(msg);
                _allMessages.Sort((a, b) =>
                {
                    int dateCompare = b.Date.CompareTo(a.Date);
                    if (dateCompare != 0) return dateCompare;

                    int idCompare = b.Id.CompareTo(a.Id);
                    if (idCompare != 0) return idCompare;

                    return string.Compare(b.Message, a.Message, StringComparison.Ordinal);
                });
            }

            ApplyFiltering();
        });
    }

    private ScrollViewer? GetScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer) return (ScrollViewer)element;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    private void ApplyFiltering(bool clear = false)
    {
        var scrollViewer = GetScrollViewer(MessagesListView);
        double verticalOffset = scrollViewer?.VerticalOffset ?? 0;
        var query = _currentSearchQuery.Trim().ToLower();
        var filteredList = new List<PushoverMessageEventArgs>();

        lock (_lockObj)
        {
            foreach (var msg in _allMessages.Where(msg => !msg.ExpirationDate.HasValue || msg.ExpirationDate >= DateTime.Now))
            {
                if (_currentAppFilter != "All" && !msg.Application.Equals(_currentAppFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(query))
                {
                    bool match = msg.Application.ToLower().Contains(query) ||
                                 msg.Title?.ToLower().Contains(query) == true ||
                                 msg.Message.ToLower().Contains(query);
                    if (!match) continue;
                }

                filteredList.Add(msg);
            }
        }
        
        if (clear)
        {
            DisplayedMessages.Clear();
            foreach (var msg in filteredList)
            {
                DisplayedMessages.Add(msg);
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                scrollViewer?.ChangeView(null, verticalOffset, null);
            });
            return;
        }

        // SMART SYNC: Incremental updates instead of Clear() + Add() loop to prevent layout jumps and crashes
        // 1. Remove items that are no longer in the filtered view
        for (int i = DisplayedMessages.Count - 1; i >= 0; i--)
        {
            if (!filteredList.Contains(DisplayedMessages[i]))
            {
                DisplayedMessages.RemoveAt(i);
            }
        }

        // 2. Add or reposition items safely
        for (int i = 0; i < filteredList.Count; i++)
        {
            var item = filteredList[i];
            if (i >= DisplayedMessages.Count)
            {
                DisplayedMessages.Add(item);
            }
            else if (DisplayedMessages[i] != item)
            {
                int oldIndex = DisplayedMessages.IndexOf(item);
                if (oldIndex >= 0)
                {
                    // FIXED: WinUI 3 has a known native crash bug with ObservableCollection.Move() in virtualized lists.
                    // Using RemoveAt + Insert completely bypasses the buggy C++ Move event handler safely.
                    DisplayedMessages.RemoveAt(oldIndex);
                    DisplayedMessages.Insert(i, item);
                }
                else
                {
                    DisplayedMessages.Insert(i, item);
                }
            }
        }

        RefreshVisibleTimestamps();

        DispatcherQueue.TryEnqueue(() =>
        {
            scrollViewer?.ChangeView(null, verticalOffset, null);
        });
    }

    private void MessagesListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Phase != 0) return;

        var msg = args.Item as PushoverMessageEventArgs;
        var templateRoot = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
        if (msg == null || templateRoot == null) return;

        var border = templateRoot.FindName("CardBorder") as Border;
        var appIconImage = templateRoot.FindName("AppIconImage") as Image;
        var fallbackTextBlock = templateRoot.FindName("FallbackTextBlock") as TextBlock;
        var bodyRichTextBlock = templateRoot.FindName("BodyRichTextBlock") as RichTextBlock;
        var hyperlinkButton = templateRoot.FindName("CardHyperlinkButton") as HyperlinkButton;
        var timeTextBlock = templateRoot.FindName("TimeTextBlock") as TextBlock;
        var deleteButton = templateRoot.FindName("DeleteButton") as Button;

        if (border != null)
        {
            var backgroundColor = msg.Priority > 0
                ? Windows.UI.Color.FromArgb(255, 60, 30, 30)
                : (msg.Priority < 0 ? Windows.UI.Color.FromArgb(255, 20, 20, 20) : Windows.UI.Color.FromArgb(255, 30, 30, 30));

            border.Background = new SolidColorBrush(backgroundColor);
            border.BorderBrush = msg.Priority > 0
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 40, 40))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 45, 45));
        }

        if (appIconImage != null && fallbackTextBlock != null)
        {
            if (!string.IsNullOrEmpty(msg.IconUrl))
            {
                appIconImage.Visibility = Visibility.Visible;
                fallbackTextBlock.Visibility = Visibility.Collapsed;
                appIconImage.Source = new BitmapImage(new Uri(msg.IconUrl));
                appIconImage.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 32, 32) };
            }
            else
            {
                appIconImage.Visibility = Visibility.Collapsed;
                fallbackTextBlock.Visibility = Visibility.Visible;
                fallbackTextBlock.Text = !string.IsNullOrEmpty(msg.Application) ? msg.Application[0].ToString().ToUpper() : "P";
            }
        }

        if (bodyRichTextBlock != null)
        {
            RichTextHelper.ParseTextWithLinks(bodyRichTextBlock, msg.Message);
        }

        if (hyperlinkButton != null)
        {
            if (!string.IsNullOrWhiteSpace(msg.Url))
            {
                hyperlinkButton.Visibility = Visibility.Visible;
                try
                {
                    hyperlinkButton.NavigateUri = new Uri(msg.Url);
                    hyperlinkButton.Content = !string.IsNullOrWhiteSpace(msg.UrlTitle) ? msg.UrlTitle : msg.Url;
                }
                catch (Exception ex)
                {
                    AddLogLine($"[UI] Failed to set hyperlink: {ex.Message}");
                    hyperlinkButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                hyperlinkButton.Visibility = Visibility.Collapsed;
            }
        }

        if (timeTextBlock != null)
        {
            timeTextBlock.Text = App.Config.Current.UseRelativeTime
                ? GetRelativeTimeString(msg.Date)
                : msg.Date.ToString("HH:mm");
        }

        if (deleteButton != null)
        {
            deleteButton.Click -= DeleteButton_Click;
            deleteButton.Tag = msg;
            deleteButton.Click += DeleteButton_Click;

            deleteButton.PointerEntered -= DeleteButton_PointerEntered;
            deleteButton.PointerExited -= DeleteButton_PointerExited;
            deleteButton.PointerEntered += DeleteButton_PointerEntered;
            deleteButton.PointerExited += DeleteButton_PointerExited;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PushoverMessageEventArgs msg)
        {
            try
            {
                _storage.DeleteMessage(msg.Id);
                lock (_lockObj)
                {
                    _allMessages.Remove(msg);
                    _processedMessageIds.Remove(msg.Id);
                }
                ApplyFiltering();
            }
            catch (Exception ex)
            {
                AddLogLine($"[UI] Error on safe message deletion execution: {ex.Message}");
            }
        }
    }

    private void DeleteButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button btn) btn.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
    }

    private void DeleteButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button btn) btn.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DimGray);
    }

    private void AllMessagesButton_Click(object sender, RoutedEventArgs e)
    {
        _currentAppFilter = "All";
        ApplyFiltering(true);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _currentSearchQuery = SearchBox.Text;
        ApplyFiltering();
    }

    public void AddLogLine(string text)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            var logTxt = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(6, 1, 6, 1),
                Opacity = 0.7,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray)
            };

            LogsContainerPanel.Children.Add(logTxt);
            LogScrollViewer.UpdateLayout();
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);

            if (LogsContainerPanel.Children.Count > 150)
            {
                LogsContainerPanel.Children.RemoveAt(0);
            }
        });
    }

    private async void StartClient_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_configFilePath))
        {
            AddLogLine($"[Config] Setup template file generation...");
            var sampleConfig = new PushoverConfig();
            sampleConfig.Pushover.Email = "your_email@example.com";
            sampleConfig.Pushover.Password = "your_password";

            string jsonTemplate = JsonSerializer.Serialize(sampleConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, jsonTemplate);

            AddLogLine($"[Config] Generated empty configuration at: {_configFilePath}");
            AddLogLine("[Config] Please set your email/password in it, then click Connect!");
            return;
        }

        try
        {
            string json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<PushoverConfig>(json);

            if (config?.Pushover == null || string.IsNullOrEmpty(config.Pushover.Email) || string.IsNullOrEmpty(config.Pushover.Password))
            {
                AddLogLine("[Error] Please fill your 'Email' and 'Password' in appsettings.json first!");
                return;
            }

            string deviceId = string.Empty;
            string secret = string.Empty;

            if (File.Exists(_cacheFilePath))
            {
                try
                {
                    string cacheJson = File.ReadAllText(_cacheFilePath);
                    var cacheTokens = JsonSerializer.Deserialize<PushoverCacheTokens>(cacheJson);
                    if (cacheTokens != null)
                    {
                        deviceId = cacheTokens.DeviceId;
                        secret = cacheTokens.Secret;
                    }
                }
                catch (Exception ex)
                {
                    AddLogLine($"[Cache] Failed to read corrupted token cache file: {ex.Message}. Re-authenticating...");
                }
            }

            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(secret))
            {
                AddLogLine("[Auth] Session tokens missing. Contacting Pushover API endpoint for authentication...");
                var tokens = await PerformBackgroundRegistrationAsync(config.Pushover.Email, config.Pushover.Password);

                if (!string.IsNullOrEmpty(tokens.deviceId) && !string.IsNullOrEmpty(tokens.secret))
                {
                    deviceId = tokens.deviceId;
                    secret = tokens.secret;

                    var cacheToSave = new PushoverCacheTokens { DeviceId = deviceId, Secret = secret };
                    string updatedCacheJson = JsonSerializer.Serialize(cacheToSave, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_cacheFilePath, updatedCacheJson);

                    AddLogLine("[Auth] Dynamic device registration tokens cached securely in AppData!");
                }
                else
                {
                    return;
                }
            }

            StartClient_Internal(deviceId, secret);
        }
        catch (Exception ex)
        {
            AddLogLine($"[Configuration Fault] {ex.Message}");
        }
    }

    private async Task<(string deviceId, string secret)> PerformBackgroundRegistrationAsync(string email, string password)
    {
        try
        {
            using var client = new HttpClient();
            var loginData = new Dictionary<string, string> { { "email", email }, { "password", password } };

            using var loginContent = new FormUrlEncodedContent(loginData);
            var loginResponse = await client.PostAsync("https://api.pushover.net/1/users/login.json", loginContent);
            string loginResultJson = await loginResponse.Content.ReadAsStringAsync();

            using var loginDoc = JsonDocument.Parse(loginResultJson);
            var loginRoot = loginDoc.RootElement;

            if (!loginRoot.TryGetProperty("status", out var status) || status.GetInt32() != 1)
            {
                AddLogLine("[Auth Error] Pushover API rejected the email or password provided.");
                return (string.Empty, string.Empty);
            }

            string rawSecret = loginRoot.GetProperty("secret").GetString() ?? string.Empty;
            string networkMachineName = Environment.MachineName + "_WinUI3";

            var deviceData = new Dictionary<string, string> { { "secret", rawSecret }, { "name", networkMachineName }, { "os", "O" } };

            using var deviceContent = new FormUrlEncodedContent(deviceData);
            var deviceResponse = await client.PostAsync("https://api.pushover.net/1/devices.json", deviceContent);
            string deviceResultJson = await deviceResponse.Content.ReadAsStringAsync();

            using var deviceDoc = JsonDocument.Parse(deviceResultJson);
            var deviceRoot = deviceDoc.RootElement;

            if (!deviceRoot.TryGetProperty("status", out var devStatus) || devStatus.GetInt32() != 1)
            {
                if (deviceRoot.TryGetProperty("errors", out var errors))
                {
                    AddLogLine($"[Registration Error] {errors.GetRawText()}");
                }
                return (string.Empty, string.Empty);
            }

            string assignedDeviceId = deviceRoot.GetProperty("id").GetString() ?? string.Empty;
            return (assignedDeviceId, rawSecret);
        }
        catch (Exception ex)
        {
            AddLogLine($"[Network Identity Error] {ex.Message}");
            return (string.Empty, string.Empty);
        }
    }

    private void StartClient_Internal(string deviceId, string secret)
    {
        _cts?.Cancel();
        _wsService = new PushoverWebSocketService(deviceId, secret, _storage);
        _wsService.OnLog += (s, msg) => AddLogLine(msg);
        _wsService.OnMessageReceived += (s, args) =>
        {
            AddMessageCard(args, isHistory: false);
        };

        _cts = new CancellationTokenSource();
        Task.Run(() => _wsService.StartAsync(_cts.Token));
        AddLogLine("[Client] Persistent WebSocket stream established using cached endpoint profile.");
    }

    private void SimulateNotification_Click(object sender, RoutedEventArgs e)
    {
        AddMessageCard(new PushoverMessageEventArgs
        {
            Id = 0,
            Title = "Production Cluster Node Critical Link Failure",
            Message = "Core network path dropped interface packet state metrics. Check pipeline visualization graph.",
            Application = "Grafana",
            IconUrl = "pushover",
            Date = DateTime.Now,
            IsRealTime = true,
            Url = "https://github.com",
            UrlTitle = "Open GitHub Pipeline"
        });
        AddLogLine("[Simulate] Injected active hypermedia verification template into UI display queue.");
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWin = new SettingsWindow();
        settingsWin.Activate();
    }

    private string GetRelativeTimeString(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;
        if (span.TotalDays >= 365) return $"{(int)(span.TotalDays / 365)}y ago";
        if (span.TotalDays >= 30) return $"{(int)(span.TotalDays / 30)}mo ago";
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d ago";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m ago";
        return "just now";
    }

    private FrameworkElement? FindVisualChildByName(DependencyObject obj, string name)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is FrameworkElement fe && fe.Name == name)
                return fe;

            var result = FindVisualChildByName(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void RefreshVisibleTimestamps()
    {
        foreach (var item in DisplayedMessages)
        {
            var container = MessagesListView.ContainerFromItem(item) as ListViewItem;
            if (container != null)
            {
                var timeTextBlock = FindVisualChildByName(container, "TimeTextBlock") as TextBlock;
                if (timeTextBlock != null)
                {
                    timeTextBlock.Text = App.Config.Current.UseRelativeTime
                        ? GetRelativeTimeString(item.Date)
                        : item.Date.ToString("HH:mm");
                }
            }
        }
    }

    private void RemoveExpiredMessages()
    {
        var now = DateTime.Now;
        var expiredItems = new List<PushoverMessageEventArgs>();

        lock (_lockObj)
        {
            foreach (var item in _allMessages)
            {
                if (item.ExpirationDate.HasValue && item.ExpirationDate < now)
                {
                    expiredItems.Add(item);
                }
            }
        }

        foreach (var item in expiredItems)
        {
            if (DisplayedMessages.Contains(item))
            {
                DisplayedMessages.Remove(item);
            }
        }
    }
}