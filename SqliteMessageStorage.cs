using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace PushoverDesktopClient;

// SQLite alapú megvalósítás (Frissítve a tiszta RAW tároláshoz)
public class SqliteMessageStorage : IMessageStorage
{
    private readonly string _dbPath = "messages.db";

    public SqliteMessageStorage()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS Messages (Id INTEGER PRIMARY KEY, RawJson TEXT)";
        command.ExecuteNonQuery();
    }

    public void SaveMessage(long id, string rawJson)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO Messages (Id, RawJson) VALUES ($id, $json)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$json", rawJson); // Itt a 100%-os tiszta API JSON string kerül mentésre
        cmd.ExecuteNonQuery();
    }

    public List<PushoverMessageEventArgs> LoadAllMessages()
    {
        var list = new List<PushoverMessageEventArgs>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT RawJson FROM Messages";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                string rawJson = reader.GetString(0);
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
                        //Ttl = item.ttl,
                        Url = item.Url,
                        UrlTitle = item.UrlTitle,
                        IconUrl = item.Icon,
                        IsRealTime = false, // A DB-ből betöltött üzeneteknél ez false, így nem triggerel hangot/toastot indításkor
                        Date = (item.Date).LocalDateTime,
                        ExpirationDate = item.ExpirationDate?.LocalDateTime
                    });
                }
            }
            catch { /* Hibás JSON rekordok átugrása */ }
        }
        return list;
    }

    public void DeleteMessage(long id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Messages WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}