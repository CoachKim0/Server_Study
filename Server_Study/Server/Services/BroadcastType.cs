namespace Server.Grpc.Services;

/// <summary>
/// 브로드캐스트 타입 정의
/// 현재 사용 중인 브로드캐스트 패턴들만 포함
/// </summary>
public enum BroadcastType
{
    /// <summary>
    /// 특정 방의 모든 사용자에게 브로드캐스트
    /// </summary>
    ToRoom,
    
    /// <summary>
    /// 특정 방의 모든 사용자에게 브로드캐스트 (특정 사용자 제외)
    /// </summary>
    ToRoomExceptUser
}