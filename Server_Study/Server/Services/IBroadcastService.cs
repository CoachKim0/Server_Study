using GrpcApp;
using Server_Study.Shared.Model;

namespace Server.Grpc.Services;

/// <summary>
/// 메시지 브로드캐스트를 담당하는 서비스 인터페이스
/// </summary>
public interface IBroadcastService
{
    /// <summary>
    /// 타입별 브로드캐스트 (분기 형식)
    /// </summary>
    /// <param name="type">브로드캐스트 타입</param>
    /// <param name="message">전송할 메시지</param>
    /// <param name="target">브로드캐스트 대상 정보</param>
    Task Broadcast(BroadcastType type, GameMessage message, BroadcastTarget target);
    
    /// <summary>
    /// 클라이언트를 등록합니다
    /// </summary>
    void RegisterClient(string clientId, ClientInfo clientInfo);
    
    /// <summary>
    /// 클라이언트를 제거합니다
    /// </summary>
    void UnregisterClient(string clientId);
}