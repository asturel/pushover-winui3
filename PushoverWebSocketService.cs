using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace PushoverDesktopClient;

public class PushoverWebSocketService
{
    private const string WsUrl = "wss://client.pushover.net/push";
    private ClientWebSocket? _webSocket;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMessageStorage _storage;
    private readonly ILogger<PushoverWebSocketService> _logger;

    public event EventHandler<PushoverMessageEventArgs>? OnMessageReceived;
    public event EventHandler<string>? OnLog;

    public PushoverWebSocketService(IHttpClientFactory httpClientFactory, IMessageStorage storage, ILogger<PushoverWebSocketService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
        _logger = logger;
    }

    public async Task StartAsync(string deviceId, string secret, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Log("Connecting to Pushover WebSocket server...");
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri(WsUrl), cancellationToken);

                Log("Connected successfully. Authenticating device...");
                await AuthenticateAsync(deviceId, secret, cancellationToken);

                Log("Authenticated! Fetching initial/backlog messages silently...");
                await FetchAndClearMessagesAsync(deviceId, secret, isRealTime: false, cancellationToken: cancellationToken);

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

    private async Task AuthenticateAsync(string deviceId, string secret, CancellationToken cancellationToken)
    {
        if (_webSocket == null) return;
        string authPayload = $"login:{deviceId}:{secret}\n";
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

            if (result.EndOfMessage && result.Count == 1)
            {
                char controlChar = (char)buffer[0];
                await ProcessControlCharAsync(controlChar);
            }
            else
            {
                string rawFrame;
                if (result.EndOfMessage)
                {
                    rawFrame = Encoding.UTF8.GetString(buffer, 0, result.Count);
                }
                else
                {
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

    private Task ProcessControlCharAsync(char controlChar)
    {
        switch (controlChar)
        {
            case '#':
                Log("Keep-alive heartbeat received (#)");
                break;
            case '!':
                Log("New message notification trigger received (!). Fetching payload...");
                // For "!" we should fetch using known credentials; callers pass device/secret into StartAsync
                break;
            case 'A':
                Log("Session closed. Device logged in from another location (A).");
                break;
            default:
                Log($"Unknown single-byte control frame received: {controlChar}");
                break;
        }
        return Task.CompletedTask;
    }

    private async Task ProcessRawFrameAsync(string frame)
    {
        if (string.IsNullOrWhiteSpace(frame)) return;

        if (frame.StartsWith("E", StringComparison.Ordinal))
        {
            Log($"Pushover Protocol Error: {frame}");
            return;
        }

        if (frame.Length == 1)
        {
            await ProcessControlCharAsync(frame[0]);
            return;
        }

        Log($"Received unhandled multi-byte frame: {frame}");
    }

    public async Task FetchAndClearMessagesAsync(string? deviceId, string? secret, bool isRealTime, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(secret))
            {
                Log("Fetch skipped: deviceId/secret missing.");
                return;
            }

            var client = _httpClientFactory.CreateClient("Pushover");
            string syncUrl = $"https://api.pushover.net/1/messages.json?secret={secret}&device_id={deviceId}";
            string responseJson = await client.GetStringAsync(syncUrl, cancellationToken);

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
                    Title = msg.Title,
                    Message = msg.Message,
                    Application = msg.Application,
                    Priority = msg.Priority,
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
                string clearUrl = $"https://api.pushover.net/1/devices/{deviceId}/update_highest_message.json";
                var clearData = new Dictionary<string, string>
                {
                    { "secret", secret! },
                    { "message", highestMessageId.ToString() }
                };

                using var clearContent = new FormUrlEncodedContent(clearData);
                var clearResponse = await client.PostAsync(clearUrl, clearContent, cancellationToken);

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

    private void Log(string message)
    {
        _logger.LogInformation(message);
        OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
