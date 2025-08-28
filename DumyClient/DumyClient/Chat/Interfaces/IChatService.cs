using DummyClient.Chat.Common;

namespace DummyClient.Chat.Interfaces;

/// <summary>
/// 채팅 서비스 인터페이스
/// TCP와 gRPC 채팅 서비스가 이를 구현합니다
/// </summary>
public interface IChatService
{
    Task<bool> ConnectAsync(string serverAddress, int port);
    Task DisconnectAsync();
    Task<bool> JoinRoomAsync(string roomId, string userName);
    Task<bool> LeaveRoomAsync();
    Task<bool> SendMessageAsync(string message);
    
    bool IsConnected { get; }
    string CurrentRoom { get; }
    string UserName { get; }
    
    event Action<ChatEventArgs> OnMessageReceived;
    event Action<ChatEventArgs> OnUserJoined;
    event Action<ChatEventArgs> OnUserLeft;
    event Action<string> OnDisconnected;
}