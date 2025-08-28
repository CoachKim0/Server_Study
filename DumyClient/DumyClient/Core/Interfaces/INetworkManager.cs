namespace DummyClient.Core.Interfaces;

/// <summary>
/// 네트워크 매니저 인터페이스
/// TCP와 gRPC 구현체가 이를 구현합니다
/// </summary>
public interface INetworkManager
{
    Task<bool> ConnectAsync(string host, int port);
    Task DisconnectAsync();
    Task<bool> SendMessageAsync(string message);
    bool IsConnected { get; }
    event Action<string> OnMessageReceived;
    event Action OnDisconnected;
}