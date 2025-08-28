using System.Collections.Concurrent;

namespace Server_Study.Shared.Model;

/// <summary>
/// 채팅방 정보를 저장하는 클래스
/// - 방별 사용자 목록과 정보를 관리
/// </summary>
public class ChatRoom
{
    public string RoomId { get; set; } = "";          // 방 ID
    public string RoomName { get; set; } = "";        // 방 이름
    public ConcurrentDictionary<string, ClientInfo> Users { get; set; } = new();  // 방에 참여중인 사용자들
    public DateTime CreatedAt { get; set; }           // 방 생성 시간
    public DateTime LastMessageAt { get; set; }       // 마지막 메시지 시간
}