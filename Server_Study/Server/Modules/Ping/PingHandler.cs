using GrpcApp;
using Microsoft.Extensions.Logging;

namespace Server_Study.Modules.Ping;

/// <summary>
/// Ping 처리 핸들러
/// - 연결 상태 확인, 지연시간 측정 등 Ping 관련 로직을 처리
/// </summary>
public class PingHandler : IPingHandler
{
    private readonly ILogger<PingHandler> _logger;

    public PingHandler(ILogger<PingHandler> logger)
    {
        _logger = logger;
    }

    public GameMessage ProcessPing(GameMessage request)
    {
        var response = new GameMessage
        {
            UserId = request.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "Pong";
        response.Ping = new global::GrpcApp.Ping
        {
            SeqNo = request.Ping.SeqNo
        };

        _logger.LogDebug($"[PingHandler] Ping 응답: SeqNo={request.Ping.SeqNo}");
        return response;
    }
}