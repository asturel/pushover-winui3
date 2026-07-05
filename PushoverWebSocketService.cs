using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PushoverDesktopClient;

// ==========================================
// PUSHOVER WEBSOCKET SERVICE
// ==========================================
public class PushoverWebSocketService
{
    private const string WsUrl = "wss://client.pushover.net/push";
    private readonly string _deviceId;
    private readonly string _secret;
    private ClientWebSocket? _webSocket;
    private readonly HttpClient _httpClient;
    private readonly IMessageStorage _storage;

    public event EventHandler<PushoverMessageEventArgs>? OnMessageReceived;
    public event EventHandler<string>? OnLog;

    public PushoverWebSocketService(string deviceId, string secret, IMessageStorage storage)
    {
        _deviceId = deviceId;
        _secret = secret;
        _storage = storage;

        // Configure HttpClient for optimal connection pooling and reuse
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            EnableMultipleHttp2Connections = true
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Log("Connecting to Pushover WebSocket server...");
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri(WsUrl), cancellationToken);

                Log("Connected successfully. Authenticating device...");
                await AuthenticateAsync(cancellationToken);

                Log("Authenticated! Fetching initial/backlog messages silently...");
                await FetchAndClearMessagesAsync(isRealTime: false);

                Log("Ready to receive real-time notifications.");
                await ReceiveLoopAsync(cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Log($"Connection error: {ex.Message}. Retrying in 5 seconds...");
                await Task.Delay(5000, cancellationToken);
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
            }
        }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null) return;
        string authPayload = $"login:{_deviceId}:{_secret}\n";
        byte[] bytes = Encoding.UTF8.GetBytes(authPayload);

        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken
        );
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        // 1KB buffer is plenty since Pushover control frames are only a few bytes
        var buffer = new byte[1024];

        while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Log("Server requested connection closure.");
                break;
            }

            if (result.Count == 0) continue;

            // ALLOCATION-FREE OPTIMIZATION:
            // If the received message is 1 byte (like '#' or '!'), evaluate the byte immediately
            // without creating a string or MemoryStream, saving the GC from hundreds of thousands of daily allocations.
            if (result.EndOfMessage && result.Count == 1)
            {
                char controlChar = (char)buffer[0];
                await ProcessControlCharAsync(controlChar);
            }
            else
            {
                // Rare case: if a longer message arrives (e.g., error starting with 'E') or arrives in multiple parts
                string rawFrame;
                if (result.EndOfMessage)
                {
                    rawFrame = Encoding.UTF8.GetString(buffer, 0, result.Count);
                }
                else
                {
                    // If it didn't fit into a single frame (very rare for Pushover control frames), append them
                    using var ms = new MemoryStream();
                    ms.Write(buffer, 0, result.Count);
                    while (!result.EndOfMessage)
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        ms.Write(buffer, 0, result.Count);
                    }
                    rawFrame = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                }
                await ProcessRawFrameAsync(rawFrame);
            }
        }
    }

    // Fast path for the most common 1-byte frames (Zero-allocation)
    private async Task ProcessControlCharAsync(char controlChar)
    {
        switch (controlChar)
        {
            case '#':
                Log("Keep-alive heartbeat received (#)");
                break;
            case '!':
                Log("New message notification trigger received (!). Fetching payload...");
                await FetchAndClearMessagesAsync(isRealTime: true);
                break;
            case 'A':
                Log("Session closed. Device logged in from another location (A).");
                break;
            default:
                Log($"Unknown single-byte control frame received: {controlChar}");
                break;
        }
    }

    // Slow path for text-based or complex frames (e.g., error messages)
    private async Task ProcessRawFrameAsync(string frame)
    {
        if (string.IsNullOrWhiteSpace(frame)) return;

        if (frame.StartsWith("E", StringComparison.Ordinal))
        {
            Log($"Pushover Protocol Error: {frame}");
            return;
        }

        // If 'A', '!' or '#' happened to arrive as a longer string frame
        if (frame.Length == 1)
        {
            await ProcessControlCharAsync(frame[0]);
            return;
        }

        Log($"Received unhandled multi-byte frame: {frame}");
    }

    public async Task FetchAndClearMessagesAsync(bool isRealTime)
    {
        try
        {
            string syncUrl = $"https://api.pushover.net/1/messages.json?secret={_secret}&device_id={_deviceId}";
            string responseJson = await _httpClient.GetStringAsync(syncUrl);

            PushoverMessageResponse? response = JsonSerializer.Deserialize(
                responseJson,
                PushoverJsonContext.Default.PushoverMessageResponse
            );

            if (response == null || response.Status != 1 || response.Messages.Count == 0)
            {
                return;
            }

            long highestMessageId = 0;

            foreach (JsonElement msgElement in response.Messages)
            {
                string rawJson = msgElement.GetRawText();
                var msg = JsonSerializer.Deserialize(
                    rawJson,
                    PushoverJsonContext.Default.PushoverMessageItem
                ) ?? throw new InvalidOperationException("Failed to deserialize PushoverMessageItem.");

                if (msg.Id > highestMessageId)
                {
                    highestMessageId = msg.Id;
                }

                var pushoverMessageEventArgs = new PushoverMessageEventArgs
                {
                    Id = msg.Id,
                    //Title = !string.IsNullOrEmpty(msg.title) ? msg.title : msg.app,
                    Title = msg.Title,
                    Message = msg.Message,
                    Application = msg.Application,
                    Priority = msg.Priority,
                    //Ttl = msg.Ttl,
                    Url = msg.Url,
                    UrlTitle = msg.UrlTitle,
                    IconUrl = msg.Icon,
                    IsRealTime = isRealTime,
                    Date = msg.Date.LocalDateTime,
                    ExpirationDate = msg.ExpirationDate?.LocalDateTime
                };

                _storage.SaveMessage(msg.Id, rawJson);
                OnMessageReceived?.Invoke(this, pushoverMessageEventArgs);
                Log($"Received message ID: {msg.Id}, Title: {msg.Title}, Date: {pushoverMessageEventArgs.Date}, App: {msg.Application}, IsRealTime: {isRealTime}");
            }

            if (highestMessageId > 0)
            {
                string clearUrl = $"https://api.pushover.net/1/devices/{_deviceId}/update_highest_message.json";
                var clearData = new Dictionary<string, string>
                {
                    { "secret", _secret },
                    { "message", highestMessageId.ToString() }
                };

                using var clearContent = new FormUrlEncodedContent(clearData);
                var clearResponse = await _httpClient.PostAsync(clearUrl, clearContent);

                if (clearResponse.IsSuccessStatusCode)
                {
                    Log($"Cleaned backend backlog queue up to message ID: {highestMessageId}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[Sync Error] Failed to download or acknowledge message payload: {ex.Message}");
        }
    }

    private void Log(string message) => OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
}