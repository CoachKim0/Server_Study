using GrpcApp;
using Server.Grpc.Services;
using Server_Study.Managers;
using Server_Study.Modules.Auth.Login;
using Server_Study.Modules.Auth.Register;
using Server_Study.Shared.Model;
using Microsoft.Extensions.Logging;

namespace Server_Study.Modules.Auth;

/// <summary>
/// 인증 처리 핸들러
/// - 로그인, 회원가입 등 인증 관련 로직을 처리
/// </summary>
public class AuthHandler : IAuthHandler
{
    private readonly ILogger<AuthHandler> _logger;

    public AuthHandler(ILogger<AuthHandler> logger)
    {
        _logger = logger;
    }

    public async Task<GameMessage> ProcessAuthUser(GameMessage request, ClientInfo clientInfo)
    {
        var response = new GameMessage
        {
            UserId = request.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var authRequest = request.AuthUser;
        
        _logger.LogInformation($"인증 요청: UserId={request.UserId}, Platform={authRequest.PlatformType}");

        GameMessage authResponse;

        // PlatformType에 따라 로그인/회원가입 구분
        if (authRequest.PlatformType == 1) // 로그인
        {
            authResponse = UserLogin.ProcessLogin(authRequest);
            _logger.LogInformation($"로그인 처리 결과: {authResponse.ResultMessage}");
        }
        else if (authRequest.PlatformType == 2) // 회원가입
        {
            authResponse = UserRegister.ProcessRegister(authRequest);
            _logger.LogInformation($"회원가입 처리 결과: {authResponse.ResultMessage}");
        }
        else
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "지원되지 않는 플랫폼 타입입니다";
            return response;
        }

        // 인증/회원가입 성공시 클라이언트 정보 업데이트
        if (authResponse.ResultCode == (int)ResultCode.Success)
        {
            clientInfo.IsAuthenticated = true;
            clientInfo.UserId = authResponse.UserId;
            clientInfo.PassKey = authResponse.Token;
            clientInfo.SubPassKey = GenerateSubPassKey();

            // UserManager에도 등록
            UserManager.Instance.AuthenticateUser(authResponse.UserId);

            _logger.LogInformation($"인증 성공: {authResponse.UserId}");
        }

        // 응답 데이터 복사
        response.ResultCode = authResponse.ResultCode;
        response.ResultMessage = authResponse.ResultMessage;
        response.UserId = authResponse.UserId;
        response.Token = authResponse.Token;
        response.AuthUser = new AuthUser
        {
            PlatformType = authRequest.PlatformType,
            RetPassKey = authResponse.Token,
            RetSubPassKey = clientInfo.SubPassKey
        };

        return response;
    }

    private string GenerateSubPassKey()
    {
        // 실제로는 더 복잡한 서브키 생성 로직 필요
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..16];
    }
}