using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.Infrastructure.Network;

public class PhoneSyncServer : IPhoneSyncServer
{
    private readonly ILogger<PhoneSyncServer> _logger;
    private readonly object _lock = new();
    private readonly List<PhoneSyncItem> _pcItems = new();
    private const int MaxPcItems = 20;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string _localIp = "127.0.0.1";
    private int _port = 5892;
    private bool _isRunning;

    public string ServerUrl => $"http://{_localIp}:{_port}";
    public string LocalIpAddress => _localIp;
    public int Port => _port;
    public bool IsRunning => _isRunning;

    public event Action<string, string>? TextReceived;
    public event Action<string>? FileReceived;

    public PhoneSyncServer(ILogger<PhoneSyncServer> logger)
    {
        _logger = logger;
        _localIp = ResolveLocalIpAddress();
    }

    public void Start()
    {
        if (_isRunning) return;

        _cts = new CancellationTokenSource();
        int basePort = 5892;

        for (int p = basePort; p < basePort + 10; p++)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, p);
                _listener.Start();
                _port = p;
                _isRunning = true;
                _logger.LogInformation("Phone Sync server started on {Url}", ServerUrl);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Port {Port} unavailable for Phone Sync: {Msg}", p, ex.Message);
            }
        }

        if (_isRunning && _listener != null)
        {
            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch { }
    }

    public void PushTextFromPc(string text, string type = "Text", string sourceApp = "PC")
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        lock (_lock)
        {
            string trimmed = text.Trim();

            // Avoid consecutive identical entries
            if (_pcItems.Count > 0 && _pcItems[0].Text == trimmed)
            {
                _pcItems[0].Timestamp = DateTime.UtcNow;
                return;
            }

            var item = new PhoneSyncItem
            {
                Text = trimmed,
                Type = type,
                SourceApp = sourceApp,
                Timestamp = DateTime.UtcNow
            };

            _pcItems.Insert(0, item);
            if (_pcItems.Count > MaxPcItems)
            {
                _pcItems.RemoveAt(_pcItems.Count - 1);
            }
        }

        _logger.LogInformation("Pushed PC clip to Phone Sync ({Type}): {Snippet}", type, text.Length > 30 ? text[..30] + "..." : text);
    }

    public string? GetLatestPcText()
    {
        lock (_lock)
        {
            return _pcItems.Count > 0 ? _pcItems[0].Text : null;
        }
    }

    public IReadOnlyList<PhoneSyncItem> GetRecentPcItems()
    {
        lock (_lock)
        {
            return _pcItems.ToList();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Error accepting client connection");
                }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                string? requestLine = await reader.ReadLineAsync(token);
                if (string.IsNullOrEmpty(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                string method = parts[0].ToUpperInvariant();
                string path = parts[1];

                // Read headers
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(token)))
                {
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                    }
                }

                int contentLength = 0;
                if (headers.TryGetValue("Content-Length", out string? lenStr))
                {
                    int.TryParse(lenStr, out contentLength);
                }

                string contentType = headers.TryGetValue("Content-Type", out string? ct) ? ct : string.Empty;

                if (method == "OPTIONS")
                {
                    await SendResponseAsync(stream, "204 No Content", "text/plain", string.Empty);
                }
                else if (method == "GET" && (path == "/" || path.StartsWith("/?")))
                {
                    await SendHtmlResponseAsync(stream, GetWebUiHtml());
                }
                else if (method == "GET" && path == "/api/status")
                {
                    await SendJsonResponseAsync(stream, new
                    {
                        status = "online",
                        url = ServerUrl,
                        app = "NOVA Clip",
                        machineName = Environment.MachineName
                    });
                }
                else if (method == "GET" && (path == "/api/pc-clips" || path == "/api/from-pc"))
                {
                    var items = GetRecentPcItems().Select(i => new
                    {
                        id = i.Id,
                        text = i.Text,
                        type = i.Type,
                        sourceApp = i.SourceApp,
                        timestamp = i.Timestamp.ToString("o"),
                        timeAgo = FormatTimeAgo(i.Timestamp)
                    });

                    await SendJsonResponseAsync(stream, new
                    {
                        success = true,
                        latest = GetLatestPcText(),
                        items
                    });
                }
                else if (method == "POST" && path == "/api/clip")
                {
                    // iPhone -> PC
                    string body = await ReadBodyAsync(reader, contentLength);
                    string text = ExtractTextFromBody(body, contentType);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        TextReceived?.Invoke(text.Trim(), "iPhone");
                        await SendJsonResponseAsync(stream, new { success = true, message = "Copied to PC clipboard!" });
                    }
                    else
                    {
                        await SendJsonResponseAsync(stream, new { success = false, message = "Empty content received" }, 400);
                    }
                }
                else if (method == "POST" && (path == "/api/to-phone" || path == "/api/send-to-phone"))
                {
                    // PC -> iPhone
                    string body = await ReadBodyAsync(reader, contentLength);
                    string text = ExtractTextFromBody(body, contentType);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        bool isUrl = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                     text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                        PushTextFromPc(text.Trim(), isUrl ? "Link" : "Text", "Web User");
                        await SendJsonResponseAsync(stream, new { success = true, message = "Sent to phone!" });
                    }
                    else
                    {
                        await SendJsonResponseAsync(stream, new { success = false, message = "Empty content" }, 400);
                    }
                }
                else if (method == "POST" && path == "/api/upload")
                {
                    // Handle file upload from phone
                    byte[] fileBytes;
                    using (var ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192];
                        int remaining = contentLength;
                        while (remaining > 0)
                        {
                            int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), token);
                            if (read == 0) break;
                            ms.Write(buffer, 0, read);
                            remaining -= read;
                        }
                        fileBytes = ms.ToArray();
                    }

                    string savedPath = SaveUploadedFile(fileBytes, contentType);
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        FileReceived?.Invoke(savedPath);
                        await SendJsonResponseAsync(stream, new { success = true, filename = Path.GetFileName(savedPath) });
                    }
                    else
                    {
                        await SendJsonResponseAsync(stream, new { success = false, message = "Failed to save file" }, 500);
                    }
                }
                else
                {
                    await SendResponseAsync(stream, "404 Not Found", "text/plain", "Not Found");
                }
            }
            catch { }
        }
    }

    private static async Task<string> ReadBodyAsync(StreamReader reader, int contentLength)
    {
        if (contentLength <= 0) return string.Empty;
        char[] buffer = new char[contentLength];
        int read = await reader.ReadBlockAsync(buffer, 0, contentLength);
        return new string(buffer, 0, read);
    }

    private static string ExtractTextFromBody(string body, string contentType)
    {
        if (contentType.Contains("application/json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    return textProp.GetString() ?? string.Empty;
                }
            }
            catch { }
        }
        else if (contentType.Contains("application/x-www-form-urlencoded"))
        {
            var match = Regex.Match(body, @"(?:^|&)text=([^&]*)");
            if (match.Success)
            {
                return Uri.UnescapeDataString(match.Groups[1].Value.Replace('+', ' '));
            }
        }
        return body;
    }

    private string SaveUploadedFile(byte[] rawData, string contentType)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NOVA.Clip", "received_files");
            Directory.CreateDirectory(dir);

            string filename = $"Phone_Transfer_{DateTime.Now:yyyyMMdd_HHmmss}.bin";

            if (contentType.Contains("boundary="))
            {
                string text = Encoding.Latin1.GetString(rawData);
                var fnMatch = Regex.Match(text, @"filename=""([^""]+)""");
                if (fnMatch.Success)
                {
                    filename = Path.GetFileName(fnMatch.Groups[1].Value);
                }

                int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd > 0)
                {
                    int bodyStart = headerEnd + 4;
                    int bodyEnd = text.LastIndexOf("\r\n--", StringComparison.Ordinal);
                    if (bodyEnd > bodyStart)
                    {
                        int len = bodyEnd - bodyStart;
                        byte[] extracted = new byte[len];
                        Array.Copy(rawData, bodyStart, extracted, 0, len);
                        string filePath = Path.Combine(dir, filename);
                        File.WriteAllBytes(filePath, extracted);
                        return filePath;
                    }
                }
            }

            string targetPath = Path.Combine(dir, filename);
            File.WriteAllBytes(targetPath, rawData);
            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file uploaded from phone");
            return string.Empty;
        }
    }

    private static async Task SendJsonResponseAsync(Stream stream, object obj, int statusCode = 200)
    {
        string json = JsonSerializer.Serialize(obj);
        string statusText = statusCode == 200 ? "200 OK" : $"{statusCode} Error";
        await SendResponseAsync(stream, statusText, "application/json; charset=utf-8", json);
    }

    private static async Task SendHtmlResponseAsync(Stream stream, string html)
    {
        await SendResponseAsync(stream, "200 OK", "text/html; charset=utf-8", html);
    }

    private static async Task SendResponseAsync(Stream stream, string status, string contentType, string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        string responseHeader = $"HTTP/1.1 {status}\r\n" +
                                $"Content-Type: {contentType}\r\n" +
                                $"Content-Length: {bytes.Length}\r\n" +
                                "Access-Control-Allow-Origin: *\r\n" +
                                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                                "Access-Control-Allow-Headers: Content-Type\r\n" +
                                "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(responseHeader);
        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private static string ResolveLocalIpAddress()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ip = addr.Address.ToString();
                        if (!ip.StartsWith("169.254.") && !ip.StartsWith("127."))
                        {
                            return ip;
                        }
                    }
                }
            }
        }
        catch { }

        return "127.0.0.1";
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.UtcNow - dt;
        if (span.TotalSeconds < 30) return "Just now";
        if (span.TotalMinutes < 1) return $"{Math.Max(1, (int)span.TotalSeconds)}s ago";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        return dt.ToString("MMM d");
    }

    private static string GetWebUiHtml()
    {
        return """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
            <title>NOVA Clip • Two-Way Sync</title>
            <style>
                :root {
                    --bg: #F8FAFC;
                    --card: #FFFFFF;
                    --card-inner: #F1F5F9;
                    --border: #E2E8F0;
                    --border-focus: #3B82F6;
                    --text-primary: #0F172A;
                    --text-secondary: #475569;
                    --text-muted: #64748B;
                    --accent: #2563EB;
                    --accent-hover: #1D4ED8;
                    --accent-light: #EFF6FF;
                    --success: #10B981;
                    --success-bg: #ECFDF5;
                    --success-border: #A7F3D0;
                    --shadow: 0 4px 20px -2px rgba(0, 0, 0, 0.06), 0 2px 6px -1px rgba(0, 0, 0, 0.04);
                }
                * {
                    box-sizing: border-box;
                    margin: 0;
                    padding: 0;
                    -webkit-tap-highlight-color: transparent;
                    font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                }
                body {
                    background: var(--bg);
                    color: var(--text-primary);
                    padding: 16px 14px 40px;
                    min-height: 100vh;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                }
                .container {
                    width: 100%;
                    max-width: 500px;
                }

                /* Header */
                .header {
                    text-align: center;
                    margin-bottom: 18px;
                }
                .logo-box {
                    width: 52px;
                    height: 52px;
                    background: #FFFFFF;
                    border: 1px solid var(--border);
                    border-radius: 14px;
                    margin: 0 auto 10px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
                }
                .logo-box svg {
                    width: 28px;
                    height: 28px;
                }
                h1 {
                    font-size: 20px;
                    font-weight: 700;
                    color: var(--text-primary);
                    letter-spacing: -0.3px;
                }
                p.sub {
                    font-size: 13px;
                    color: var(--text-muted);
                    margin-top: 2px;
                }
                .status-pill {
                    display: inline-flex;
                    align-items: center;
                    gap: 6px;
                    background: #ECFDF5;
                    border: 1px solid var(--success-border);
                    color: #047857;
                    font-size: 11.5px;
                    font-weight: 600;
                    padding: 4px 10px;
                    border-radius: 20px;
                    margin-top: 8px;
                }
                .status-dot {
                    width: 7px;
                    height: 7px;
                    background: var(--success);
                    border-radius: 50%;
                    box-shadow: 0 0 6px rgba(16, 185, 129, 0.6);
                }

                /* Notification Banners */
                .banner {
                    display: none;
                    padding: 12px 14px;
                    border-radius: 12px;
                    font-size: 13px;
                    font-weight: 500;
                    text-align: center;
                    margin-bottom: 14px;
                    animation: fadeIn 0.2s ease;
                }
                .banner-success {
                    background: var(--success-bg);
                    border: 1px solid var(--success-border);
                    color: #065F46;
                }
                @keyframes fadeIn {
                    from { opacity: 0; transform: translateY(-4px); }
                    to { opacity: 1; transform: translateY(0); }
                }

                /* Cards */
                .card {
                    background: var(--card);
                    border: 1px solid var(--border);
                    border-radius: 18px;
                    padding: 18px 16px;
                    margin-bottom: 16px;
                    box-shadow: var(--shadow);
                }
                .card-header {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    margin-bottom: 12px;
                }
                .section-badge {
                    display: inline-flex;
                    align-items: center;
                    gap: 5px;
                    padding: 4px 9px;
                    border-radius: 7px;
                    font-size: 11px;
                    font-weight: 700;
                    letter-spacing: 0.4px;
                    text-transform: uppercase;
                }
                .badge-from-pc {
                    background: #EFF6FF;
                    color: #1D4ED8;
                }
                .badge-to-pc {
                    background: #F1F5F9;
                    color: #334155;
                }
                .badge-live {
                    font-size: 10px;
                    font-weight: 600;
                    color: var(--text-muted);
                }

                /* Content Box for Received from PC */
                .clip-preview-box {
                    background: var(--card-inner);
                    border: 1px solid var(--border);
                    border-radius: 12px;
                    padding: 14px;
                    margin-bottom: 12px;
                    max-height: 160px;
                    overflow-y: auto;
                    font-size: 14px;
                    line-height: 1.45;
                    word-break: break-word;
                    color: var(--text-primary);
                }
                .clip-meta {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    font-size: 11px;
                    color: var(--text-muted);
                    margin-bottom: 6px;
                }
                .clip-type-pill {
                    font-size: 9.5px;
                    font-weight: 700;
                    padding: 2px 6px;
                    border-radius: 4px;
                    text-transform: uppercase;
                }
                .type-link { background: #E0F2FE; color: #0369A1; }
                .type-text { background: #EDE9FE; color: #6D28D9; }

                /* Buttons */
                .btn {
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    gap: 7px;
                    width: 100%;
                    padding: 13px 16px;
                    border-radius: 12px;
                    font-size: 14.5px;
                    font-weight: 600;
                    cursor: pointer;
                    border: none;
                    transition: all 0.15s ease;
                }
                .btn:active {
                    transform: scale(0.98);
                }
                .btn-copy {
                    background: #059669;
                    color: #FFFFFF;
                }
                .btn-copy:hover {
                    background: #047857;
                }
                .btn-copy.copied {
                    background: #10B981;
                }
                .btn-open {
                    background: #FFFFFF;
                    color: var(--accent);
                    border: 1px solid #BFDBFE;
                    margin-top: 8px;
                }
                .btn-open:hover {
                    background: #EFF6FF;
                }
                .btn-primary {
                    background: var(--accent);
                    color: #FFFFFF;
                    margin-top: 10px;
                }
                .btn-primary:hover {
                    background: var(--accent-hover);
                }
                .btn-secondary {
                    background: #FFFFFF;
                    color: var(--text-secondary);
                    border: 1px solid var(--border);
                    margin-top: 8px;
                }
                .btn-secondary:hover {
                    background: #F8FAFC;
                }
                .btn-file {
                    background: #F8FAFC;
                    border: 1.5px dashed var(--border);
                    color: var(--text-muted);
                    margin-top: 10px;
                    padding: 11px;
                    font-size: 13px;
                }
                .btn-file:hover {
                    border-color: var(--accent);
                    color: var(--accent);
                }

                /* Form Controls */
                textarea {
                    width: 100%;
                    height: 110px;
                    background: var(--card-inner);
                    border: 1px solid var(--border);
                    border-radius: 12px;
                    color: var(--text-primary);
                    padding: 12px 14px;
                    font-size: 15px;
                    line-height: 1.4;
                    resize: none;
                    outline: none;
                    transition: border-color 0.2s;
                }
                textarea:focus {
                    border-color: var(--accent);
                    background: #FFFFFF;
                }

                /* History List for PC Clips */
                .history-toggle {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    font-size: 12px;
                    font-weight: 600;
                    color: var(--text-muted);
                    padding: 8px 0 4px;
                    cursor: pointer;
                    user-select: none;
                }
                .history-list {
                    margin-top: 8px;
                    border-top: 1px solid var(--border);
                    padding-top: 8px;
                }
                .history-item {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    padding: 9px 10px;
                    background: var(--card-inner);
                    border: 1px solid var(--border);
                    border-radius: 10px;
                    margin-bottom: 6px;
                    font-size: 12.5px;
                }
                .history-item-text {
                    flex: 1;
                    white-space: nowrap;
                    overflow: hidden;
                    text-overflow: ellipsis;
                    margin-right: 10px;
                    color: var(--text-primary);
                }
                .btn-mini-copy {
                    padding: 5px 9px;
                    background: #FFFFFF;
                    border: 1px solid var(--border);
                    border-radius: 7px;
                    font-size: 11px;
                    font-weight: 600;
                    color: var(--accent);
                    cursor: pointer;
                    white-space: nowrap;
                }
                .btn-mini-copy:active {
                    background: #EFF6FF;
                }

                /* Footer */
                .footer {
                    text-align: center;
                    font-size: 11px;
                    color: var(--text-muted);
                    margin-top: 8px;
                }
                .empty-hint {
                    text-align: center;
                    color: var(--text-muted);
                    font-size: 13px;
                    padding: 20px 10px;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <!-- Header -->
                <div class="header">
                    <div class="logo-box">
                        <svg viewBox="0 0 24 24" fill="none" stroke="#0F172A" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                        </svg>
                    </div>
                    <h1>NOVA Clip • Two-Way Sync</h1>
                    <p class="sub">Instant Local Wi-Fi Bridge (PC ⇄ iPhone)</p>
                    <div class="status-pill">
                        <span class="status-dot"></span>
                        <span id="connStatus">Connected &amp; Live</span>
                    </div>
                </div>

                <!-- Toast Banner -->
                <div id="statusBanner" class="banner banner-success"></div>

                <!-- ================= SECTION 1: RECEIVED FROM PC (PC -> iPhone) ================= -->
                <div class="card">
                    <div class="card-header">
                        <span class="section-badge badge-from-pc">📥 Received from PC</span>
                        <span class="badge-live" id="liveSyncTag">🔄 Live Feed</span>
                    </div>

                    <div id="pcClipContainer">
                        <div class="clip-meta">
                            <span id="pcClipType" class="clip-type-pill type-text">TEXT</span>
                            <span id="pcClipTime">Just now</span>
                        </div>
                        <div id="pcClipPreview" class="clip-preview-box">
                            Waiting for text or links from your PC...
                        </div>

                        <button id="btnCopyFromPc" class="btn btn-copy" onclick="copyPcClipToPhone()">
                            📋 Copy to iPhone Clipboard
                        </button>

                        <button id="btnOpenLink" class="btn btn-open" style="display:none" onclick="openPcLink()">
                            🔗 Open Link in Safari
                        </button>
                    </div>

                    <!-- Collapsible Recent PC History -->
                    <div class="history-toggle" onclick="toggleHistory()">
                        <span>Recent Clips from PC (<span id="historyCount">0</span>)</span>
                        <span id="historyArrow">▼</span>
                    </div>
                    <div id="historyList" class="history-list" style="display:none">
                        <!-- Populated dynamically -->
                    </div>
                </div>

                <!-- ================= SECTION 2: SEND TO PC (iPhone -> PC) ================= -->
                <div class="card">
                    <div class="card-header">
                        <span class="section-badge badge-to-pc">📤 Send to PC</span>
                        <span class="badge-live">Top Island Dock</span>
                    </div>

                    <textarea id="textInput" placeholder="Paste links, websites, or text from your phone here..."></textarea>
                    
                    <button class="btn btn-primary" onclick="sendTextToPc()">
                        🚀 Send to PC Clipboard
                    </button>
                    
                    <button class="btn btn-secondary" onclick="pasteFromPhoneAndSend()">
                        📋 Paste from iPhone &amp; Send
                    </button>

                    <input type="file" id="fileInput" style="display:none" onchange="uploadFile(this)">
                    <button class="btn btn-file" onclick="document.getElementById('fileInput').click()">
                        📁 Or tap to send a Photo / File to PC
                    </button>
                </div>

                <div class="footer">
                    100% Offline • Protected Local Wi-Fi • No Cloud
                </div>
            </div>

            <script>
                let currentPcText = '';
                let lastSeenId = '';
                let isHistoryOpen = false;

                // Load initial PC clips & start real-time polling
                fetchPcClips();
                setInterval(fetchPcClips, 2500);

                async function fetchPcClips() {
                    try {
                        const res = await fetch('/api/pc-clips');
                        if (!res.ok) return;
                        const data = await res.json();

                        if (data && data.items && data.items.length > 0) {
                            const latest = data.items[0];
                            document.getElementById('historyCount').innerText = data.items.length;

                            if (latest.id !== lastSeenId) {
                                lastSeenId = latest.id;
                                currentPcText = latest.text;

                                // Update main preview
                                document.getElementById('pcClipPreview').innerText = latest.text;
                                document.getElementById('pcClipTime').innerText = latest.timeAgo || 'Just now';

                                const typeBadge = document.getElementById('pcClipType');
                                const isOpenable = isUrl(latest.text);

                                if (isOpenable) {
                                    typeBadge.innerText = 'LINK';
                                    typeBadge.className = 'clip-type-pill type-link';
                                    document.getElementById('btnOpenLink').style.display = 'flex';
                                } else {
                                    typeBadge.innerText = (latest.type || 'TEXT').toUpperCase();
                                    typeBadge.className = 'clip-type-pill type-text';
                                    document.getElementById('btnOpenLink').style.display = 'none';
                                }

                                document.getElementById('btnCopyFromPc').style.display = 'flex';
                            }

                            // Render history items
                            renderHistory(data.items);
                        } else {
                            document.getElementById('pcClipPreview').innerHTML = '<div class="empty-hint">Copy anything on your PC (or right-click a slot on Top Island and choose Send to Phone) and it will appear here instantly.</div>';
                            document.getElementById('btnCopyFromPc').style.display = 'none';
                            document.getElementById('btnOpenLink').style.display = 'none';
                        }
                    } catch (e) {
                        // Connection retry
                    }
                }

                function renderHistory(items) {
                    const list = document.getElementById('historyList');
                    list.innerHTML = '';
                    
                    items.forEach((item, index) => {
                        const row = document.createElement('div');
                        row.className = 'history-item';
                        
                        const textSpan = document.createElement('span');
                        textSpan.className = 'history-item-text';
                        textSpan.innerText = item.text;
                        
                        const copyBtn = document.createElement('button');
                        copyBtn.className = 'btn-mini-copy';
                        copyBtn.innerText = '📋 Copy';
                        copyBtn.onclick = () => copySpecificText(item.text, copyBtn);

                        row.appendChild(textSpan);
                        row.appendChild(copyBtn);
                        list.appendChild(row);
                    });
                }

                function toggleHistory() {
                    isHistoryOpen = !isHistoryOpen;
                    document.getElementById('historyList').style.display = isHistoryOpen ? 'block' : 'none';
                    document.getElementById('historyArrow').innerText = isHistoryOpen ? '▲' : '▼';
                }

                async function copyPcClipToPhone() {
                    if (!currentPcText) return;
                    await performCopy(currentPcText, document.getElementById('btnCopyFromPc'));
                }

                async function copySpecificText(text, buttonElement) {
                    await performCopy(text, buttonElement);
                }

                async function performCopy(text, buttonElement) {
                    try {
                        if (navigator.clipboard && navigator.clipboard.writeText) {
                            await navigator.clipboard.writeText(text);
                        } else {
                            // Fallback
                            const ta = document.createElement('textarea');
                            ta.value = text;
                            ta.style.position = 'fixed';
                            ta.style.opacity = '0';
                            document.body.appendChild(ta);
                            ta.focus();
                            ta.select();
                            document.execCommand('copy');
                            document.body.removeChild(ta);
                        }

                        // Haptic feedback if available
                        if (navigator.vibrate) navigator.vibrate(40);

                        // Visual feedback on button
                        const oldText = buttonElement.innerText;
                        buttonElement.innerText = '✓ Copied to iPhone!';
                        buttonElement.classList.add('copied');
                        showBanner('✓ Copied to your iPhone clipboard!');

                        setTimeout(() => {
                            buttonElement.innerText = oldText;
                            buttonElement.classList.remove('copied');
                        }, 2200);
                    } catch (err) {
                        alert('Could not copy automatically. Tap and hold the text box to copy manually.');
                    }
                }

                function openPcLink() {
                    if (!currentPcText) return;
                    let url = currentPcText.trim();
                    if (!url.startsWith('http://') && !url.startsWith('https://')) {
                        url = 'https://' + url;
                    }
                    window.open(url, '_blank');
                }

                function isUrl(str) {
                    if (!str) return false;
                    const s = str.trim();
                    return s.startsWith('http://') || s.startsWith('https://') || /^[a-zA-Z0-9-]+\.[a-zA-Z]{2,}(\/.*)?$/.test(s);
                }

                // Send to PC
                async function sendTextToPc() {
                    const text = document.getElementById('textInput').value.trim();
                    if (!text) {
                        alert('Please enter or paste something first.');
                        return;
                    }

                    try {
                        const res = await fetch('/api/clip', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ text: text })
                        });

                        const data = await res.json();
                        if (data.success) {
                            showBanner('🚀 Sent to PC! Now in your PC clipboard & Top Island.');
                            document.getElementById('textInput').value = '';
                        } else {
                            alert(data.message || 'Error sending to PC');
                        }
                    } catch (e) {
                        alert('Could not reach PC. Make sure both devices are on the same Wi-Fi.');
                    }
                }

                async function pasteFromPhoneAndSend() {
                    try {
                        if (navigator.clipboard && navigator.clipboard.readText) {
                            const clipText = await navigator.clipboard.readText();
                            if (clipText) {
                                document.getElementById('textInput').value = clipText;
                                await sendTextToPc();
                                return;
                            }
                        }
                    } catch (e) { }

                    const textarea = document.getElementById('textInput');
                    textarea.focus();
                    textarea.placeholder = "Tap here and choose 'Paste', then tap Send.";
                }

                async function uploadFile(input) {
                    if (!input.files || input.files.length === 0) return;
                    const file = input.files[0];

                    const formData = new FormData();
                    formData.append('file', file);

                    showBanner('Uploading ' + file.name + ' to PC...');

                    try {
                        const res = await fetch('/api/upload', {
                            method: 'POST',
                            body: formData
                        });
                        const data = await res.json();
                        if (data.success) {
                            showBanner('📁 File ' + data.filename + ' sent to your PC!');
                        } else {
                            alert('Upload failed');
                        }
                    } catch (e) {
                        alert('Could not send file to PC.');
                    }
                }

                function showBanner(msg) {
                    const banner = document.getElementById('statusBanner');
                    banner.innerText = msg;
                    banner.style.display = 'block';
                    setTimeout(() => { banner.style.display = 'none'; }, 4000);
                }
            </script>
        </body>
        </html>
        """;
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
