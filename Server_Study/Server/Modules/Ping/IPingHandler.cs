using GrpcApp;

namespace Server_Study.Modules.Ping;

/// <summary>
/// Ping 처리를 담당하는 핸들러 인터페이스
/// - 연결 상태 확인, 지연시간 측정 등 Ping 관련 로직 처리
/// </summary>
public interface IPingHandler
{
    /// <summary>
    /// Ping 요청 처리
    /// </summary>
    /// <param name="request">Ping 요청</param>
    /// <returns>처리 결과 메시지</returns>
    GameMessage ProcessPing(GameMessage request);
}