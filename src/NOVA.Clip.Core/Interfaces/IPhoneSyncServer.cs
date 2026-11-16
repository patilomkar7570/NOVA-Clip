namespace NOVA.Clip.Core.Interfaces;

public interface IPhoneSyncServer : IDisposable
{
    string ServerUrl { get; }
    string LocalIpAddress { get; }
    int Port { get; }
    bool IsRunning { get; }

    void Start();
    void Stop();

    event Action<string, string>? TextReceived;
    event Action<string>? FileReceived;
}
