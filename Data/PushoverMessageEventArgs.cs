namespace PushoverDesktopClient;

// ==========================================
// 1. DATA MODELS & EVENT ARGS
// ==========================================
public class PushoverMessageEventArgs : EventArgs
{
    private string? iconUrl;

    public long Id { get; set; }
    public string? Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Application { get; set; } = string.Empty;
    //public string IconUrl { get; set; } = string.Empty;
    public string IconUrl
    {
        get
        {
            var iconName = !string.IsNullOrWhiteSpace(iconUrl) ? iconUrl : "pushover";
            var url = $"https://pushover.net/icons/{iconName}.png";
            return url;
        }
        set
        {
            iconUrl = value;
        }
    }
    public int Priority { get; set; }
    //public int Ttl { get; set; }
    public string? Url { get; set; } = string.Empty;
    public string? UrlTitle { get; set; } = string.Empty;
    public bool IsRealTime { get; set; }
    public DateTime Date { get; set; }
    public DateTime? ExpirationDate { get; set; }
}
