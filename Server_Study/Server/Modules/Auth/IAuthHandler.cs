using GrpcApp;
using Server.Grpc.Services;
using Server_Study.Shared.Model;

namespace Server_Study.Modules.Auth;

/// <summary>
/// 인증 처리를 담당하는 핸들러 인터페이스
/// - 로그인, 회원가입 등 인증 관련 로직 처리
/// </summary>
public interface IAuthHandler
{
    /// <summary>
    /// 인증 요청 처리 (로그인/회원가입)
    /// </summary>
    /// <param name="request">인증 요청</param>
    /// <param name="clientInfo">클라이언트 정보</param>
    /// <returns>처리 결과 메시지</returns>
    Task<GameMessage> ProcessAuthUser(GameMessage request, ClientInfo clientInfo);
}