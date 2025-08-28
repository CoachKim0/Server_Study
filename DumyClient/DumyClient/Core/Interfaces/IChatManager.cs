namespace DummyClient.Core.Interfaces;

/// <summary>
/// 채팅 매니저 인터페이스
/// TCP와 gRPC 채팅 기능이 이를 구현합니다
/// </summary>
public interface IChatManager
{
    Task<bool> JoinRoomAsync(string roomId, string userName);
    Task<bool> LeaveRoomAsync();
    Task<bool> SendMessageAsync(string message);
    string CurrentRoom { get; }
    event Action<string> OnChatReceived;
    event Action<string> OnUserJoined;
    event Action<string> OnUserLeft;
}