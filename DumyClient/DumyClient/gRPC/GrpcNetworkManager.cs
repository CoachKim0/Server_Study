using DummyClient.Core.Interfaces;

namespace DummyClient.gRPC;

/// <summary>
/// gRPC 네트워크 매니저 구현체
/// </summary>
public class GrpcNetworkManager : INetworkManager
{
    public bool IsConnected { get; private set; }
    
    public event Action<string>? OnMessageReceived;
    public event Action? OnDisconnected;

    public async Task<bool> ConnectAsync(string host, int port)
    {
        // TODO: gRPC 연결 구현
        await Task.Delay(100); // 임시
        IsConnected = true;
        return true;
    }

    public async Task DisconnectAsync()
    {
        // TODO: gRPC 연결 해제 구현
        await Task.Delay(100); // 임시
        IsConnected = false;
        OnDisconnected?.Invoke();
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        // TODO: gRPC 메시지 전송 구현
        await Task.Delay(100); // 임시
        return IsConnected;
    }
}