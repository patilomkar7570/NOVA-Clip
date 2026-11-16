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

                if (method == "GET" && (path == "/" || path.StartsWith("/?")))
                {
                    await SendHtmlResponseAsync(stream, GetWebUiHtml());
                }
                else if (method == "GET" && path == "/api/status")
                {
                    await SendJsonResponseAsync(stream, new { status = "online", url = ServerUrl, app = "NOVA Clip" });
                }
                else if (method == "POST" && path == "/api/clip")
                {
                    string body = string.Empty;
                    if (contentLength > 0)
                    {
                        char[] buffer = new char[contentLength];
                        int read = await reader.ReadBlockAsync(buffer, 0, contentLength);
                        body = new string(buffer, 0, read);
                    }

                    string text = string.Empty;
                    if (contentType.Contains("application/json"))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("text", out var textProp))
                            {
                                text = textProp.GetString() ?? string.Empty;
                            }
                        }
                        catch { }
                    }
                    else if (contentType.Contains("application/x-www-form-urlencoded"))
                    {
                        var match = Regex.Match(body, @"(?:^|&)text=([^&]*)");
                        if (match.Success)
                        {
                            text = Uri.UnescapeDataString(match.Groups[1].Value.Replace('+', ' '));
                        }
                    }
                    else
                    {
                        text = body;
                    }

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

    private string SaveUploadedFile(byte[] rawData, string contentType)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NOVA.Clip", "received_files");
            Directory.CreateDirectory(dir);

            string filename = $"Phone_Transfer_{DateTime.Now:yyyyMMdd_HHmmss}.bin";

            // If multipart/form-data, extract filename and clean content
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

    private static string GetWebUiHtml()
    {
        return """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
            <title>NOVA Clip • Phone to PC</title>
            <style>
                :root {
                    --bg: #0D0E12;
                    --card: #181920;
                    --border: #282A36;
                    --accent: #2563EB;
                    --accent-hover: #1D4ED8;
                    --text: #F3F4F6;
                    --text-muted: #9CA3AF;
                    --success: #10B981;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; -webkit-tap-highlight-color: transparent; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
                body { background: var(--bg); color: var(--text); padding: 20px 16px; min-height: 100vh; display: flex; flex-direction: column; align-items: center; }
                .container { width: 100%; max-width: 480px; }
                .header { text-align: center; margin-bottom: 24px; }
                .logo-circle { width: 54px; height: 54px; border-radius: 50%; border: 3px solid #FFF; margin: 0 auto 12px; display: flex; align-items: center; justify-content: center; position: relative; }
                .logo-dot { width: 12px; height: 12px; background: #FFF; border-radius: 50%; position: absolute; right: 10px; top: 18px; }
                h1 { font-size: 22px; font-weight: 700; margin-bottom: 4px; }
                p.sub { font-size: 13px; color: var(--text-muted); }
                .card { background: var(--card); border: 1px solid var(--border); border-radius: 16px; padding: 18px; margin-bottom: 16px; box-shadow: 0 10px 25px rgba(0,0,0,0.4); }
                textarea { width: 100%; height: 140px; background: #101116; border: 1px solid var(--border); border-radius: 12px; color: var(--text); padding: 14px; font-size: 16px; resize: none; outline: none; margin-bottom: 14px; transition: border 0.2s; }
                textarea:focus { border-color: var(--accent); }
                .btn { display: flex; align-items: center; justify-content: center; width: 100%; padding: 14px; border-radius: 12px; font-size: 15px; font-weight: 600; cursor: pointer; border: none; transition: transform 0.1s, background 0.2s; }
                .btn:active { transform: scale(0.98); }
                .btn-primary { background: var(--accent); color: white; margin-bottom: 10px; }
                .btn-primary:hover { background: var(--accent-hover); }
                .btn-secondary { background: #222530; color: var(--text); border: 1px solid var(--border); }
                .btn-file { background: transparent; border: 1px dashed var(--border); color: var(--text-muted); margin-top: 14px; padding: 12px; }
                .status-banner { display: none; background: #064E3B; border: 1px solid var(--success); color: #34D399; padding: 12px 16px; border-radius: 12px; font-size: 14px; font-weight: 500; text-align: center; margin-bottom: 16px; }
                .badge { display: inline-block; padding: 3px 8px; border-radius: 6px; font-size: 11px; font-weight: 600; background: #1E293B; color: #60A5FA; margin-bottom: 10px; }
                .footer { text-align: center; font-size: 11px; color: var(--text-muted); margin-top: auto; padding-top: 20px; }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <div class="logo-circle">
                        <div class="logo-dot"></div>
                    </div>
                    <h1>NOVA Clip • Phone Sync</h1>
                    <p class="sub">Instant transfer to your Windows PC clipboard &amp; Top Island</p>
                </div>

                <div id="statusBanner" class="status-banner">
                    ✓ Sent to your PC! It's now in your clipboard &amp; Top Island.
                </div>

                <div class="card">
                    <span class="badge">LINKS &amp; TEXT</span>
                    <textarea id="textInput" placeholder="Paste links, websites, or text from your phone here..."></textarea>
                    
                    <button class="btn btn-primary" onclick="sendText()">
                        🚀 Send to PC Clipboard
                    </button>
                    
                    <button class="btn btn-secondary" onclick="pasteFromPhone()">
                        📋 Paste from iPhone &amp; Send
                    </button>

                    <input type="file" id="fileInput" style="display:none" onchange="uploadFile(this)">
                    <button class="btn btn-file" onclick="document.getElementById('fileInput').click()">
                        📁 Or tap to send a Photo / File to PC
                    </button>
                </div>

                <div class="footer">
                    Connected via Local Wi-Fi • 100% Offline &amp; Private
                </div>
            </div>

            <script>
                async function sendText() {
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
                            showSuccess('✓ Sent! Now waiting on your PC Top Island.');
                            document.getElementById('textInput').value = '';
                        } else {
                            alert(data.message || 'Error sending to PC');
                        }
                    } catch (e) {
                        alert('Could not reach PC. Make sure both devices are on the same Wi-Fi.');
                    }
                }

                async function pasteFromPhone() {
                    try {
                        if (navigator.clipboard && navigator.clipboard.readText) {
                            const clipText = await navigator.clipboard.readText();
                            if (clipText) {
                                document.getElementById('textInput').value = clipText;
                                await sendText();
                                return;
                            }
                        }
                    } catch (e) { }

                    // Fallback: focus textarea for manual tap-paste
                    const textarea = document.getElementById('textInput');
                    textarea.focus();
                    textarea.placeholder = "Tap here and choose 'Paste', then tap Send.";
                }

                async function uploadFile(input) {
                    if (!input.files || input.files.length === 0) return;
                    const file = input.files[0];

                    const formData = new FormData();
                    formData.append('file', file);

                    showSuccess('Uploading ' + file.name + ' to PC...');

                    try {
                        const res = await fetch('/api/upload', {
                            method: 'POST',
                            body: formData
                        });
                        const data = await res.json();
                        if (data.success) {
                            showSuccess('✓ File ' + data.filename + ' sent to your PC!');
                        } else {
                            alert('Upload failed');
                        }
                    } catch (e) {
                        alert('Could not send file to PC.');
                    }
                }

                function showSuccess(msg) {
                    const banner = document.getElementById('statusBanner');
                    banner.innerText = msg;
                    banner.style.display = 'block';
                    setTimeout(() => { banner.style.display = 'none'; }, 4500);
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
