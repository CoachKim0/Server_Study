using DummyClient.Core.Interfaces;

namespace DummyClient.TCP;

/// <summary>
/// TCP 채팅 매니저 구현체
/// </summary>
public class TcpChatManager : IChatManager
{
    public string CurrentRoom { get; private set; } = "";
    
    public event Action<string>? OnChatReceived;
    public event Action<string>? OnUserJoined;
    public event Action<string>? OnUserLeft;

    public async Task<bool> JoinRoomAsync(string roomId, string userName)
    {
        // TODO: TCP 채팅방 입장 구현
        await Task.Delay(100); // 임시
        CurrentRoom = roomId;
        return true;
    }

    public async Task<bool> LeaveRoomAsync()
    {
        // TODO: TCP 채팅방 퇴장 구현
        await Task.Delay(100); // 임시
        CurrentRoom = "";
        return true;
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        // TODO: TCP 채팅 메시지 전송 구현
        await Task.Delay(100); // 임시
        return !string.IsNullOrEmpty(CurrentRoom);
    }
}