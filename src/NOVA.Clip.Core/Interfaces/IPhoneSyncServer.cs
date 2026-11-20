namespace NOVA.Clip.Core.Interfaces;

public class PhoneSyncItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "Text"; // Text, Link, File
    public string SourceApp { get; set; } = "PC";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public interface IPhoneSyncServer : IDisposable
{
    string ServerUrl { get; }
    string LocalIpAddress { get; }
    int Port { get; }
    bool IsRunning { get; }

    void Start();
    void Stop();

    // iPhone -> PC
    event Action<string, string>? TextReceived;
    event Action<string>? FileReceived;

    // PC -> iPhone (Two-Way)
    void PushTextFromPc(string text, string type = "Text", string sourceApp = "PC");
    string? GetLatestPcText();
    IReadOnlyList<PhoneSyncItem> GetRecentPcItems();
}
