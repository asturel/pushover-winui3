using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PushoverDesktopClient;

public interface IConfigService
{
    AppConfig Current { get; }
    void Save(AppConfig config);
}

public class ConfigService : IConfigService
{
    private readonly string _configFilePath;
    private readonly IOptionsMonitor<AppConfig> _options;

    public AppConfig Current { get; private set; } = null!;

    public ConfigService(IOptionsMonitor<AppConfig> options)
    {
        _options = options;
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        Current = _options.CurrentValue ?? new AppConfig();

        // When configuration reloads this will update Current automatically
        _options.OnChange(newVal =>
        {
            Current = newVal ?? new AppConfig();
            App.NotifyConfigChanged();
        });
    }

    public void Save(AppConfig config)
    {
        Current = config;
        try
        {
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);

            // The Host's configuration should be configured with reloadOnChange = true so writing the file
            // will cause the IConfiguration to reload and trigger IOptionsMonitor.OnChange above.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }

        // Notify legacy static subscribers
        App.NotifyConfigChanged();
    }
}

public class AppConfig
{
    public PushoverCredentials Pushover { get; set; } = new();
    public bool UseRelativeTime { get; set; } = false; // Directly inside the serializable model
}
