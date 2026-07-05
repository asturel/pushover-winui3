using System.Text.Json;

namespace PushoverDesktopClient;

// Fájl alapú megvalósítás (Frissítve az új interfészhez)
public class FileMessageStorage : IMessageStorage
{
    private readonly string _storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

    public FileMessageStorage() { if (!Directory.Exists(_storagePath)) Directory.CreateDirectory(_storagePath); }

    public void SaveMessage(long id, string rawJson) => 
        File.WriteAllText(Path.Combine(_storagePath, $"{id}.json"), rawJson);

    public List<PushoverMessageEventArgs> LoadAllMessages()
    {
        var list = new List<PushoverMessageEventArgs>();
        foreach (var file in Directory.GetFiles(_storagePath, "*.json"))
        {
            try
            {
                string rawJson = File.ReadAllText(file);
                //var item = JsonSerializer.Deserialize<PushoverMessageItem>(rawJson);
                var item = JsonSerializer.Deserialize(
                    rawJson, 
                    PushoverJsonContext.Default.PushoverMessageItem
                ) ?? throw new InvalidOperationException("Failed to deserialize PushoverMessageItem.");
                if (item != null)
                {
                    list.Add(new PushoverMessageEventArgs
                    {
                        Id = item.Id,
                        Title = item.Title,
                        Message = item.Message,
                        Application = item.Application,
                        Priority = item.Priority,
                        Url = item.Url,
                        UrlTitle = item.UrlTitle,
                        IconUrl = item.Icon,
                        IsRealTime = false, // Visszatöltött adat mindig hamis
                        Date = (item.Date).LocalDateTime,
                        ExpirationDate = item.ExpirationDate?.LocalDateTime
                    });
                }
            }
            catch { /* Hibás fájlok átugrása */ }
        }
        return list;
    }

    public void DeleteMessage(long id) => File.Delete(Path.Combine(_storagePath, $"{id}.json"));
}
