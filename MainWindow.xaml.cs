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
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceProvider _services;

    public MainWindow(IMessageStorage storage, IServiceProvider services)
    {
        this.InitializeComponent();
        _storage = storage;
        _services = services;

        App.ConfigChanged += () =>
        {
            // Immediately refresh the list so the Converter runs again with the new format
            ApplyFiltering();
        };

        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PushoverDesktopClient");
        Directory.CreateDirectory(appDataFolder);
        _cacheFilePath = Path.Combine(appDataFolder, "deviceid_cache.json");

        AddLogLine($"[UI] Target config path: {_configFilePath}");
        AddLogLine($"[UI] Secure token storage path: {_cacheFilePath}");

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
                RefreshVisibleTimestamps(); // Recalculate time values, scroll position doesn't jump due to UI layout preservation!
            }
            RemoveExpiredMessages();
        };
        _timeRefreshTimer.Start();

        // Subscribe to settings window save event
        App.ConfigChanged += () =>
        {
            ApplyFiltering(); // If we saved, immediately switch the format
            RefreshVisibleTimestamps(); // Recalculate time values, scroll position doesn't jump due to UI layout preservation!
        };
    }

    // ... rest of MainWindow remains unchanged except where _storage or _wsService created

    private void StartClient_Internal(string deviceId, string secret)
    {
        _cts?.Cancel();
        // Acquire singleton websocket service from DI
        _ws_service = _services.GetRequiredService<PushoverWebSocketService>();
        _ws_service.OnLog += (s, msg) => AddLogLine(msg);
        _ws_service.OnMessageReceived += (s, args) =>
        {
            AddMessageCard(args, isHistory: false);
        };

        _cts = new CancellationTokenSource();
        Task.Run(() => _ws_service.StartAsync(deviceId, secret, _cts.Token));
        AddLogLine("[Client] Persistent WebSocket stream established using cached endpoint profile.");
    }

    // keep remaining methods unchanged (DeleteButton_Click, ApplyFiltering, etc.)
}
