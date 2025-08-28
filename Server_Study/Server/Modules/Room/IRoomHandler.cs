using GrpcApp;
using Server.Grpc.Services;
using Server_Study.Shared.Model;

namespace Server_Study.Modules.Room;

/// <summary>
/// 방 관리를 담당하는 핸들러 인터페이스
/// - 방 입장, 퇴장, 목록 조회 등 방 관련 로직 처리
/// </summary>
public interface IRoomHandler
{
    /// <summary>
    /// 방 관련 요청 처리
    /// </summary>
    /// <param name="request">방 요청</param>
    /// <param name="clientInfo">클라이언트 정보</param>
    /// <param name="broadcastService">브로드캐스트 서비스</param>
    /// <returns>처리 결과 메시지</returns>
    Task<GameMessage> ProcessRoomInfo(GameMessage request, ClientInfo clientInfo, IBroadcastService broadcastService);
}