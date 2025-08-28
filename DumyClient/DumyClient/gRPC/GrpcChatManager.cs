using DummyClient.Core.Interfaces;

namespace DummyClient.gRPC;

/// <summary>
/// gRPC 채팅 매니저 구현체
/// </summary>
public class GrpcChatManager : IChatManager
{
    public string CurrentRoom { get; private set; } = "";
    
    public event Action<string>? OnChatReceived;
    public event Action<string>? OnUserJoined;
    public event Action<string>? OnUserLeft;

    public async Task<bool> JoinRoomAsync(string roomId, string userName)
    {
        // TODO: gRPC 채팅방 입장 구현
        await Task.Delay(100); // 임시
        CurrentRoom = roomId;
        return true;
    }

    public async Task<bool> LeaveRoomAsync()
    {
        // TODO: gRPC 채팅방 퇴장 구현
        await Task.Delay(100); // 임시
        CurrentRoom = "";
        return true;
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        // TODO: gRPC 채팅 메시지 전송 구현
        await Task.Delay(100); // 임시
        return !string.IsNullOrEmpty(CurrentRoom);
    }
}