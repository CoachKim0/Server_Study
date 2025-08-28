using Grpc.Core;
using GrpcApp;
using System.Collections.Concurrent;

namespace GrpcServer.Services;

public class GameGrpcService : GameService.GameServiceBase
{
    private readonly ILogger<GameGrpcService> _logger;
    private static readonly ConcurrentDictionary<string, ClientInfo> _connectedClients = new();
    private static readonly ConcurrentDictionary<string, RoomInfo> _rooms = new();

    public GameGrpcService(ILogger<GameGrpcService> logger)
    {
        _logger = logger;
    }

    public override async Task Game(IAsyncStreamReader<GameMessage> requestStream, IServerStreamWriter<GameMessage> responseStream, ServerCallContext context)
    {
        var clientId = context.GetHttpContext().Connection.Id;
        _logger.LogInformation($"\n========== 새로운 클라이언트 연결 ==========\n클라이언트 ID: {clientId}");

        var clientInfo = new ClientInfo
        {
            ClientId = clientId,
            ResponseStream = responseStream,
            ConnectedAt = DateTime.UtcNow
        };

        _connectedClients.TryAdd(clientId, clientInfo);

        try
        {
            // 클라이언트로부터 메시지 수신 처리
            await foreach (var request in requestStream.ReadAllAsync())
            {
                _logger.LogInformation($"수신된 메시지: {request.MessageTypeCase} from {request.UserId}");
                
                var response = await ProcessGameMessage(request, clientInfo);
                if (response != null)
                {
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
            // 클라이언트가 방에 있었다면 방에서 제거
            if (_connectedClients.TryGetValue(clientId, out var disconnectedClient) && 
                !string.IsNullOrEmpty(disconnectedClient.CurrentRoomId))
            {
                _logger.LogWarning($"연결 해제된 클라이언트 {disconnectedClient.UserId}를 방 {disconnectedClient.CurrentRoomId}에서 제거");
                await RemoveUserFromRoom(disconnectedClient.UserId, disconnectedClient.CurrentRoomId);
            }
            
            _connectedClients.TryRemove(clientId, out _);
            _logger.LogInformation($"❌ [클라이언트 연결 해제] {clientId} ({disconnectedClient?.UserId})\n");
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
                return await ProcessAuthUser(request, clientInfo, response);

            case GameMessage.MessageTypeOneofCase.Ping:
                return ProcessPing(request, response);

            case GameMessage.MessageTypeOneofCase.JoinRoom:
                return await ProcessJoinRoom(request, clientInfo, response);

            case GameMessage.MessageTypeOneofCase.LeaveRoom:
                return await ProcessLeaveRoom(request, clientInfo, response);

            case GameMessage.MessageTypeOneofCase.RoomMessage:
                await ProcessRoomMessage(request, clientInfo);
                return null;

            case GameMessage.MessageTypeOneofCase.Kick:
                _logger.LogWarning($"킥 메시지 수신: {request.Kick.Reason}");
                return null;

            default:
                response.ResultCode = (int)ResultCode.Fail;
                response.ResultMessage = "지원되지 않는 메시지 타입입니다";
                return response;
        }
    }

    private async Task<GameMessage> ProcessAuthUser(GameMessage request, ClientInfo clientInfo, GameMessage response)
    {
        var authRequest = request.AuthUser;
        
        _logger.LogInformation($"인증 요청: UserId={request.UserId}, Platform={authRequest.PlatformType}");

        // 간단한 인증 로직 (실제로는 DB 확인 등이 필요)
        if (!string.IsNullOrEmpty(request.UserId) && !string.IsNullOrEmpty(authRequest.AuthKey))
        {
            // 인증 성공
            var passKey = GeneratePassKey();
            var subPassKey = GenerateSubPassKey();
            
            clientInfo.IsAuthenticated = true;
            clientInfo.UserId = request.UserId;
            clientInfo.PassKey = passKey;
            clientInfo.SubPassKey = subPassKey;

            response.ResultCode = (int)ResultCode.Success;
            response.ResultMessage = "인증 성공";
            response.AuthUser = new AuthUser
            {
                PlatformType = authRequest.PlatformType,
                RetPassKey = passKey,
                RetSubPassKey = subPassKey
            };

            _logger.LogInformation($"✅ [인증 성공] {request.UserId}");
        }
        else
        {
            // 인증 실패
            response.ResultCode = (int)ResultCode.AuthenticationFailed;
            response.ResultMessage = "인증 정보가 올바르지 않습니다";
            
            _logger.LogWarning($"❌ [인증 실패] {request.UserId}");
        }

        return response;
    }

    private GameMessage ProcessPing(GameMessage request, GameMessage response)
    {
        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "Pong";
        response.Ping = new Ping
        {
            SeqNo = request.Ping.SeqNo
        };

        _logger.LogDebug($"Ping 응답: SeqNo={request.Ping.SeqNo}");
        return response;
    }

    private string GeneratePassKey()
    {
        // 실제로는 더 복잡한 키 생성 로직 필요
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..16];
    }

    private string GenerateSubPassKey()
    {
        // 실제로는 더 복잡한 서브키 생성 로직 필요
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..16];
    }

    private async Task<GameMessage> ProcessJoinRoom(GameMessage request, ClientInfo clientInfo, GameMessage response)
    {
        var joinRequest = request.JoinRoom;
        var roomId = joinRequest.RoomId;

        _logger.LogInformation($"방 입장 요청: UserId={request.UserId}, RoomId={roomId}");
        _logger.LogInformation($"현재 _rooms 딕셔너리에 있는 방들: [{string.Join(", ", _rooms.Keys)}], 총 {_rooms.Count}개");

        if (string.IsNullOrEmpty(roomId))
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "방 ID가 필요합니다";
            return response;
        }

        // 방이 존재하지 않는 경우 생성
        var room = _rooms.GetOrAdd(roomId, roomKey => 
        {
            _logger.LogInformation($"새로운 방 생성: {roomKey}");
            return new RoomInfo
            {
                RoomId = roomKey,
                RoomName = joinRequest.RoomName ?? roomKey,
                MaxUsers = joinRequest.MaxUsers > 0 ? joinRequest.MaxUsers : 10,
                Users = new ConcurrentDictionary<string, bool>()
            };
        });

        // 이미 방에 있는지 확인
        if (room.Users.ContainsKey(request.UserId))
        {
            response.ResultCode = (int)ResultCode.AlreadyInRoom;
            response.ResultMessage = "이미 방에 입장한 상태입니다";
            return response;
        }

        // 방 인원 제한 확인
        if (room.Users.Count >= room.MaxUsers)
        {
            response.ResultCode = (int)ResultCode.RoomFull;
            response.ResultMessage = "방이 가득 찼습니다";
            return response;
        }

        // 방에 사용자 추가
        room.Users.TryAdd(request.UserId, true);
        clientInfo.CurrentRoomId = roomId;
        
        _logger.LogInformation($"🚪 [방 입장] {request.UserId} → {roomId}\n   현재 방 인원: [{string.Join(", ", room.Users.Keys)}] ({room.Users.Count}명)");

        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "방 입장 성공";
        response.JoinRoom = new JoinRoom
        {
            RoomId = roomId,
            RoomName = room.RoomName,
            MaxUsers = room.MaxUsers
        };

        // 방의 다른 사용자들에게 입장 알림 브로드캐스트
        await BroadcastToRoom(roomId, new GameMessage
        {
            UserId = "System",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResultCode = (int)ResultCode.Success,
            UserJoined = new UserJoined
            {
                RoomId = roomId,
                UserId = request.UserId,
                CurrentUsers = { room.Users.Keys.ToArray() }
            }
        }, excludeUserId: request.UserId);

        _logger.LogInformation($"✅ [방 입장 성공] {request.UserId} → {roomId} (총 {room.Users.Count}명)\n");
        return response;
    }

    private async Task<GameMessage> ProcessLeaveRoom(GameMessage request, ClientInfo clientInfo, GameMessage response)
    {
        var leaveRequest = request.LeaveRoom;
        var roomId = leaveRequest.RoomId;

        if (string.IsNullOrEmpty(roomId) || !_rooms.TryGetValue(roomId, out var room))
        {
            response.ResultCode = (int)ResultCode.RoomNotFound;
            response.ResultMessage = "방을 찾을 수 없습니다";
            return response;
        }

        // 방에서 사용자 제거
        room.Users.TryRemove(request.UserId, out _);
        clientInfo.CurrentRoomId = "";

        // 방의 다른 사용자들에게 나감 알림 브로드캐스트
        await BroadcastToRoom(roomId, new GameMessage
        {
            UserId = "System",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResultCode = (int)ResultCode.Success,
            UserLeft = new UserLeft
            {
                RoomId = roomId,
                UserId = request.UserId,
                CurrentUsers = { room.Users.Keys.ToArray() }
            }
        });

        // 방이 비었으면 삭제
        if (room.Users.IsEmpty)
        {
            _rooms.TryRemove(roomId, out _);
            _logger.LogInformation($"빈 방 삭제: {roomId}");
        }

        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "방 나가기 성공";

        _logger.LogInformation($"🚪 [방 나가기] {request.UserId} ← {roomId}\n   남은 인원: [{string.Join(", ", room.Users.Keys)}] ({room.Users.Count}명)\n");
        return response;
    }

    private async Task ProcessRoomMessage(GameMessage request, ClientInfo clientInfo)
    {
        var roomMessage = request.RoomMessage;
        var roomId = roomMessage.RoomId;

        if (string.IsNullOrEmpty(roomId) || !_rooms.ContainsKey(roomId))
        {
            _logger.LogWarning($"존재하지 않는 방으로 메시지 전송 시도: {roomId}");
            return;
        }

        // 방의 모든 사용자에게 메시지 브로드캐스트
        await BroadcastToRoom(roomId, new GameMessage
        {
            UserId = request.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResultCode = (int)ResultCode.Success,
            RoomMessage = roomMessage
        });

        _logger.LogInformation($"💬 [방 메시지] {roomId}\n   보낸이: {request.UserId}\n   내용: {roomMessage.Content}\n   브로드캐스트 대상: [{string.Join(", ", _rooms[roomId].Users.Keys)}]\n");
    }

    private async Task BroadcastToRoom(string roomId, GameMessage message, string? excludeUserId = null)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            _logger.LogWarning($"방을 찾을 수 없음: {roomId}");
            return;
        }

        var tasks = new List<Task>();
        _logger.LogInformation($"방 {roomId}에 브로드캐스트 시작, 대상 사용자: [{string.Join(", ", room.Users.Keys)}], 제외: {excludeUserId}");

        foreach (var kvp in room.Users)
        {
            var userId = kvp.Key;
            if (userId == excludeUserId)
            {
                _logger.LogDebug($"사용자 {userId} 제외됨");
                continue;
            }

            var client = _connectedClients.Values.FirstOrDefault(c => c.UserId == userId);
            if (client?.ResponseStream != null)
            {
                _logger.LogDebug($"사용자 {userId}에게 메시지 전송");
                tasks.Add(SendMessageToClient(client, message));
            }
            else
            {
                _logger.LogWarning($"사용자 {userId}의 클라이언트를 찾을 수 없음 또는 스트림이 null");
            }
        }

        _logger.LogInformation($"총 {tasks.Count}개의 클라이언트에게 브로드캐스트");
        await Task.WhenAll(tasks);
    }

    private async Task RemoveUserFromRoom(string userId, string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            return;

        // 방에서 사용자 제거
        if (room.Users.TryRemove(userId, out _))
        {
            // 방의 다른 사용자들에게 나감 알림 브로드캐스트
            await BroadcastToRoom(roomId, new GameMessage
            {
                UserId = "System",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ResultCode = (int)ResultCode.Success,
                UserLeft = new UserLeft
                {
                    RoomId = roomId,
                    UserId = userId,
                    CurrentUsers = { room.Users.Keys.ToArray() }
                }
            });

            // 방이 비었으면 삭제
            if (room.Users.IsEmpty)
            {
                _rooms.TryRemove(roomId, out _);
                _logger.LogInformation($"빈 방 삭제: {roomId}");
            }
        }
    }

    private async Task SendMessageToClient(ClientInfo client, GameMessage message)
    {
        try
        {
            if (client.ResponseStream != null)
            {
                await client.ResponseStream.WriteAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"클라이언트 {client.ClientId}에게 메시지 전송 실패");
        }
    }
}

public class ClientInfo
{
    public string ClientId { get; set; } = "";
    public string UserId { get; set; } = "";
    public IServerStreamWriter<GameMessage>? ResponseStream { get; set; }
    public bool IsAuthenticated { get; set; }
    public string PassKey { get; set; } = "";
    public string SubPassKey { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
    public string CurrentRoomId { get; set; } = "";
}

public class RoomInfo
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public int MaxUsers { get; set; } = 10;
    public ConcurrentDictionary<string, bool> Users { get; set; } = new();
}