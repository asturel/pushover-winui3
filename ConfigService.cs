using System;
using System.IO;
using System.Text.Json;

namespace PushoverDesktopClient;

public interface IConfigService
{
    AppConfig Current { get; }
    void Save(AppConfig config);
}

public class ConfigService : IConfigService
{
    private readonly string _configFilePath;
    public AppConfig Current { get; private set; } = null!;

    public ConfigService()
    {
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        Load();
    }

    private void Load()
    {
        if (File.Exists(_configFilePath))
        {
            try
            {
                string json = File.ReadAllText(_configFilePath);
                Current = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            }
        }
        Current = new AppConfig();
    }

    public void Save(AppConfig config)
    {
        Current = config;
        try
        {
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}

public class AppConfig
{
    public PushoverCredentials Pushover { get; set; } = new();
    public bool UseRelativeTime { get; set; } = false;
}