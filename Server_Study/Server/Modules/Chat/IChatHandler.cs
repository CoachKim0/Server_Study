using GrpcApp;
using Server.Grpc.Services;
using Server_Study.Shared.Model;

namespace Server_Study.Modules.Chat;

/// <summary>
/// 채팅 처리를 담당하는 핸들러 인터페이스
/// - 각 컨텍스트별(전투, 길드, 파티 등) 구현체를 만들 수 있음
/// </summary>
public interface IChatHandler
{
    /// <summary>
    /// 채팅 메시지 처리
    /// </summary>
    /// <param name="request">채팅 요청</param>
    /// <param name="clientInfo">클라이언트 정보</param>
    /// <returns>처리 결과 메시지</returns>
    Task<GameMessage> ProcessChatMessage(GameMessage request, ClientInfo clientInfo);

    /// <summary>
    /// 해당 핸들러가 처리할 수 있는 채팅 타입인지 확인
    /// </summary>
    /// <param name="chatType">채팅 타입</param>
    /// <returns>처리 가능 여부</returns>
    bool CanHandle(ChatType chatType);
}