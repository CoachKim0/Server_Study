namespace Server.Grpc.Services;

/// <summary>
/// 브로드캐스트 대상 정보를 담는 클래스
/// 현재 사용 중인 패턴에 맞춰 구성
/// </summary>
public class BroadcastTarget
{
    /// <summary>
    /// 대상 방 ID (ToRoom, ToRoomExceptUser에서 사용)
    /// </summary>
    public string RoomId { get; set; } = "";
    
    /// <summary>
    /// 제외할 사용자 ID (ToRoomExceptUser에서 사용)
    /// </summary>
    public string? ExcludeUserId { get; set; }

    /// <summary>
    /// 방 전체 브로드캐스트용 생성자
    /// </summary>
    public static BroadcastTarget Room(string roomId)
    {
        return new BroadcastTarget { RoomId = roomId };
    }

    /// <summary>
    /// 특정 사용자 제외 방 브로드캐스트용 생성자
    /// </summary>
    public static BroadcastTarget RoomExceptUser(string roomId, string excludeUserId)
    {
        return new BroadcastTarget 
        { 
            RoomId = roomId, 
            ExcludeUserId = excludeUserId 
        };
    }
}