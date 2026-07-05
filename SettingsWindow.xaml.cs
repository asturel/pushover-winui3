using Microsoft.UI.Xaml;

namespace PushoverDesktopClient;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        this.InitializeComponent();
        
        // Adatok betöltése a globális ConfigService-ből
        var config = App.Config.Current;
        EmailBox.Text = config.Pushover.Email ?? string.Empty;
        PasswordBox.Password = config.Pushover.Password ?? string.Empty;
        RelativeTimeToggle.IsOn = App.Config.Current.UseRelativeTime;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var updatedConfig = new AppConfig
        {
            Pushover = new PushoverCredentials
            {
                Email = EmailBox.Text.Trim(),
                Password = PasswordBox.Password
            },
            UseRelativeTime = RelativeTimeToggle.IsOn

        };
        App.Config.Save(updatedConfig);
        
        // Értesítjük a többi ablakot a változásról
        App.NotifyConfigChanged();
        
        this.Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}