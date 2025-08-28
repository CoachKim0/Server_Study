namespace DummyClient.Chat.Common;

/// <summary>
/// 채팅 이벤트 데이터
/// </summary>
public class ChatEventArgs
{
    public string RoomId { get; set; } = "";
    public string UserId { get; set; } = "";  
    public string UserName { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public ChatEventType EventType { get; set; }
}

/// <summary>
/// 채팅 이벤트 타입
/// </summary>
public enum ChatEventType
{
    Message,
    UserJoined,
    UserLeft,
    SystemNotice
}