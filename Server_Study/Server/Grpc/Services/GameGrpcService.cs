using Grpc.Core;
using GrpcApp;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Server_Study.Managers;
using Server_Study.Modules.Auth;
using Server_Study.Modules.Room;
using Server_Study.Modules.Ping;
using Server_Study.Shared.Model;

namespace Server.Grpc.Services;

/// <summary>
/// gRPC 게임 서비스 클래스
/// - 각 클라이언트 연결마다 새로운 인스턴스가 생성됩니다
/// - static 멤버를 사용해서 모든 인스턴스가 같은 방/사용자 데이터를 공유합니다
/// </summary>
public class GameGrpcService : GameService.GameServiceBase
{
    private readonly ILogger<GameGrpcService> _logger;
    private readonly IAuthHandler _authHandler;
    private readonly IRoomHandler _roomHandler;
    private readonly IPingHandler _pingHandler;
    private readonly IBroadcastService _broadcastService;
    
    // ⭐ static으로 선언: 모든 gRPC 인스턴스가 공유하는 전역 데이터
    // - 각 클라이언트 연결마다 새 인스턴스가 생성되지만, 이 데이터는 공유됨
    private static readonly ConcurrentDictionary<string, ClientInfo> _connectedClients = new();  // 연결된 모든 클라이언트 목록
    private static readonly ConcurrentDictionary<string, ChatRoom> _chatRooms = new();           // 모든 채팅방 목록

    /// <summary>
    /// 생성자 - 새 클라이언트 연결시마다 호출됩니다
    /// </summary>
    public GameGrpcService(ILogger<GameGrpcService> logger, 
        IAuthHandler authHandler,
        IRoomHandler roomHandler,
        IPingHandler pingHandler,
        IBroadcastService broadcastService)
    {
        _logger = logger;
        _authHandler = authHandler;
        _roomHandler = roomHandler;
        _pingHandler = pingHandler;
        _broadcastService = broadcastService;
        // ❌ 여기서 딕셔너리를 초기화하면 안됩니다! (각 인스턴스마다 별도 생성됨)
        // ✅ static으로 선언했으므로 자동으로 공유됩니다
    }

    /// <summary>
    /// 메인 gRPC 스트리밍 메서드
    /// - 클라이언트와 양방향 실시간 통신을 담당
    /// - 각 클라이언트마다 별도의 스레드에서 실행됩니다
    /// </summary>
    public override async Task Game(IAsyncStreamReader<GameMessage> requestStream, IServerStreamWriter<GameMessage> responseStream, ServerCallContext context)
    {
        // 1️⃣ 클라이언트 정보 설정
        var clientId = context.GetHttpContext().Connection.Id;  // 고유한 연결 ID 생성
        _logger.LogInformation($"새로운 클라이언트 연결: {clientId}");

        // 클라이언트 정보 객체 생성 (이 클라이언트의 상태 정보 저장용)
        var clientInfo = new ClientInfo
        {
            ClientId = clientId,
            ResponseStream = responseStream,  // 🔥 중요: 클라이언트에게 메시지 보낼 때 사용
            ConnectedAt = DateTime.UtcNow
        };

        // 2️⃣ 전역 클라이언트 목록에 추가 (모든 인스턴스가 공유하는 static 데이터)
        _connectedClients.TryAdd(clientId, clientInfo);
        _broadcastService.RegisterClient(clientId, clientInfo);

        try
        {
            // 3️⃣ 클라이언트로부터 계속 메시지를 받아서 처리하는 무한 루프
            await foreach (var request in requestStream.ReadAllAsync())
            {
                _logger.LogInformation($"수신된 메시지: {request.MessageTypeCase} from {request.UserId}");
                
                // 받은 메시지 타입에 따라 처리하고 응답 생성
                var response = await ProcessGameMessage(request, clientInfo);
                if (response != null)
                {
                    // 해당 클라이언트에게만 응답 전송
                    await responseStream.WriteAsync(response);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"클라이언트 {clientId} 처리 중 오류 발생");
        }
        finally
        {
            // 4️⃣ 연결 종료시 정리 작업
            _connectedClients.TryRemove(clientId, out _);  // 전역 목록에서 제거
            _broadcastService.UnregisterClient(clientId);
            _logger.LogInformation($"클라이언트 {clientId} 연결 해제");
        }
    }

    private async Task<GameMessage?> ProcessGameMessage(GameMessage request, ClientInfo clientInfo)
    {
        var response = new GameMessage
        {
            UserId = request.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        switch (request.MessageTypeCase)
        {
            case GameMessage.MessageTypeOneofCase.AuthUser:
                return await _authHandler.ProcessAuthUser(request, clientInfo);

            case GameMessage.MessageTypeOneofCase.Ping:
                return _pingHandler.ProcessPing(request);

            case GameMessage.MessageTypeOneofCase.Kick:
                _logger.LogWarning($"킥 메시지 수신: {request.Kick.Reason}");
                return null;


            case GameMessage.MessageTypeOneofCase.RoomInfo:
                return await _roomHandler.ProcessRoomInfo(request, clientInfo, _broadcastService);

            default:
                response.ResultCode = (int)ResultCode.Fail;
                response.ResultMessage = "지원되지 않는 메시지 타입입니다";
                return response;
        }
    }

}

