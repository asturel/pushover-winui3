namespace PushoverDesktopClient;

public class CardMetadata
{
    public DateTime Date { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty; // ÚJ MEZŐ: Ide fűzzük össze a Címet és az Üzenetet!
}