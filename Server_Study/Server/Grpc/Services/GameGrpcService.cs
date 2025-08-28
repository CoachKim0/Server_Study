using Grpc.Core;
using GrpcApp;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Server_Study.Managers;

namespace Server.Grpc.Services;

/// <summary>
/// gRPC 게임 서비스 클래스
/// - 각 클라이언트 연결마다 새로운 인스턴스가 생성됩니다
/// - static 멤버를 사용해서 모든 인스턴스가 같은 방/사용자 데이터를 공유합니다
/// </summary>
public class GameGrpcService : GameService.GameServiceBase
{
    private readonly ILogger<GameGrpcService> _logger;
    
    // ⭐ static으로 선언: 모든 gRPC 인스턴스가 공유하는 전역 데이터
    // - 각 클라이언트 연결마다 새 인스턴스가 생성되지만, 이 데이터는 공유됨
    private static readonly ConcurrentDictionary<string, ClientInfo> _connectedClients = new();  // 연결된 모든 클라이언트 목록
    private static readonly ConcurrentDictionary<string, ChatRoom> _chatRooms = new();           // 모든 채팅방 목록

    /// <summary>
    /// 생성자 - 새 클라이언트 연결시마다 호출됩니다
    /// </summary>
    public GameGrpcService(ILogger<GameGrpcService> logger)
    {
        _logger = logger;
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
                return await ProcessAuthUser(request, clientInfo, response);

            case GameMessage.MessageTypeOneofCase.Ping:
                return ProcessPing(request, response);

            case GameMessage.MessageTypeOneofCase.Kick:
                _logger.LogWarning($"킥 메시지 수신: {request.Kick.Reason}");
                return null;

            case GameMessage.MessageTypeOneofCase.ChatMessage:
                return await ProcessChatMessage(request, clientInfo, response);

            case GameMessage.MessageTypeOneofCase.RoomInfo:
                return await ProcessRoomInfo(request, clientInfo, response);

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
            // UserManager를 통한 인증 처리
            bool authenticated = UserManager.Instance.AuthenticateUser(request.UserId);
            
            if (authenticated)
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

                _logger.LogInformation($"인증 성공: {request.UserId}");
            }
        }
        else
        {
            // 인증 실패
            response.ResultCode = (int)ResultCode.AuthenticationFailed;
            response.ResultMessage = "인증 정보가 올바르지 않습니다";
            
            _logger.LogWarning($"인증 실패: {request.UserId}");
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

    private async Task<GameMessage> ProcessChatMessage(GameMessage request, ClientInfo clientInfo, GameMessage response)
    {
        var chatMessage = request.ChatMessage;
        
        // 인증된 사용자인지 확인
        if (!clientInfo.IsAuthenticated)
        {
            response.ResultCode = (int)ResultCode.AuthenticationFailed;
            response.ResultMessage = "인증되지 않은 사용자입니다";
            return response;
        }

        // 채팅방이 존재하는지 확인하고, 없으면 생성
        var room = _chatRooms.GetOrAdd(chatMessage.RoomId, roomId => new ChatRoom
        {
            RoomId = roomId,
            RoomName = $"채팅방 {roomId}",
            CreatedAt = DateTime.UtcNow
        });

        // 사용자를 채팅방에 추가
        room.Users.TryAdd(clientInfo.UserId, clientInfo);
        clientInfo.CurrentRoomId = chatMessage.RoomId;

        _logger.LogInformation($"채팅 메시지: [{chatMessage.RoomId}] {chatMessage.Nickname}: {chatMessage.Content}");

        // 채팅방의 모든 사용자에게 메시지 브로드캐스트
        await BroadcastMessageToRoom(chatMessage.RoomId, new GameMessage
        {
            UserId = request.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResultCode = (int)ResultCode.Success,
            ChatMessage = new ChatMessage
            {
                RoomId = chatMessage.RoomId,
                UserId = chatMessage.UserId,
                Nickname = chatMessage.Nickname,
                Content = chatMessage.Content,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Type = ChatType.NormalChat
            }
        });

        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "메시지 전송 성공";
        return response;
    }

    private async Task<GameMessage> ProcessRoomInfo(GameMessage request, ClientInfo clientInfo, GameMessage response)
    {
        var roomInfo = request.RoomInfo;

        switch (roomInfo.Action)
        {
            case RoomAction.JoinRoom:
                return await ProcessJoinRoom(roomInfo, clientInfo, response);

            case RoomAction.LeaveRoom:
                return await ProcessLeaveRoom(roomInfo, clientInfo, response);

            case RoomAction.RoomList:
                return ProcessRoomList(response);

            default:
                response.ResultCode = (int)ResultCode.Fail;
                response.ResultMessage = "지원되지 않는 룸 액션입니다";
                return response;
        }
    }

    private async Task<GameMessage> ProcessJoinRoom(RoomInfo roomInfo, ClientInfo clientInfo, GameMessage response)
    {
        if (!clientInfo.IsAuthenticated)
        {
            response.ResultCode = (int)ResultCode.AuthenticationFailed;
            response.ResultMessage = "인증되지 않은 사용자입니다";
            return response;
        }

        // UserManager를 통한 채팅방 입장 처리
        bool joined = UserManager.Instance.JoinRoom(clientInfo.UserId, roomInfo.RoomId);
        
        if (!joined)
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "채팅방 입장에 실패했습니다";
            return response;
        }

        // 기존 ChatRoom도 유지 (호환성을 위해)
        var room = _chatRooms.GetOrAdd(roomInfo.RoomId, roomId => new ChatRoom
        {
            RoomId = roomId,
            RoomName = roomInfo.RoomName ?? $"채팅방 {roomId}",
            CreatedAt = DateTime.UtcNow
        });

        room.Users.TryAdd(clientInfo.UserId, clientInfo);
        clientInfo.CurrentRoomId = roomInfo.RoomId;

        _logger.LogInformation($"사용자 {clientInfo.UserId}이(가) 채팅방 {roomInfo.RoomId}에 입장했습니다");

        // 다른 사용자들에게 입장 알림
        await BroadcastMessageToRoom(roomInfo.RoomId, new GameMessage
        {
            UserId = "SYSTEM",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResultCode = (int)ResultCode.Success,
            ChatMessage = new ChatMessage
            {
                RoomId = roomInfo.RoomId,
                UserId = "SYSTEM",
                Nickname = "시스템",
                Content = $"{clientInfo.UserId}님이 입장했습니다.",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Type = ChatType.UserJoin
            }
        }, clientInfo.UserId); // 본인 제외

        // 기존 사용자들에게 업데이트된 사용자 목록 브로드캐스트
        var roomUsers = UserManager.Instance.GetRoomUsers(roomInfo.RoomId);
        var roomUpdateMessage = new GameMessage
        {
            UserId = "SYSTEM",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResultCode = (int)ResultCode.Success,
            RoomInfo = new RoomInfo
            {
                RoomId = roomInfo.RoomId,
                RoomName = roomInfo.RoomName ?? $"채팅방 {roomInfo.RoomId}",
                UserCount = roomUsers.Count,
                Action = RoomAction.JoinRoom
            }
        };

        // 업데이트된 사용자 목록 추가
        foreach (var user in roomUsers)
        {
            roomUpdateMessage.RoomInfo.Users.Add(user.UserId);
        }

        await BroadcastMessageToRoom(roomInfo.RoomId, roomUpdateMessage, clientInfo.UserId); // 본인 제외

        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "채팅방 입장 성공";
        
        // 최신 사용자 목록 다시 가져오기 (응답 생성 시점)
        var currentRoomUsers = UserManager.Instance.GetRoomUsers(roomInfo.RoomId);
        
        response.RoomInfo = new RoomInfo
        {
            RoomId = roomInfo.RoomId,
            RoomName = roomInfo.RoomName ?? $"채팅방 {roomInfo.RoomId}",
            UserCount = currentRoomUsers.Count,
            Action = RoomAction.JoinRoom
        };

        // 현재 채팅방 사용자 목록 추가
        foreach (var user in currentRoomUsers)
        {
            response.RoomInfo.Users.Add(user.UserId);
        }

        return response;
    }

    private async Task<GameMessage> ProcessLeaveRoom(RoomInfo roomInfo, ClientInfo clientInfo, GameMessage response)
    {
        if (string.IsNullOrEmpty(clientInfo.CurrentRoomId))
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "참여 중인 채팅방이 없습니다";
            return response;
        }

        // UserManager를 통한 채팅방 퇴장 처리
        string currentRoomId = clientInfo.CurrentRoomId;
        bool left = UserManager.Instance.LeaveRoom(clientInfo.UserId, currentRoomId);
        
        if (left)
        {
            // 다른 사용자들에게 퇴장 알림
            await BroadcastMessageToRoom(currentRoomId, new GameMessage
            {
                UserId = "SYSTEM",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ResultCode = (int)ResultCode.Success,
                ChatMessage = new ChatMessage
                {
                    RoomId = currentRoomId,
                    UserId = "SYSTEM",
                    Nickname = "시스템",
                    Content = $"{clientInfo.UserId}님이 퇴장했습니다.",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Type = ChatType.UserLeave
                }
            });

            // 기존 사용자들에게 업데이트된 사용자 목록 브로드캐스트
            var roomUsers = UserManager.Instance.GetRoomUsers(currentRoomId);
            if (roomUsers.Count > 0) // 방에 사용자가 남아있는 경우만
            {
                var roomUpdateMessage = new GameMessage
                {
                    UserId = "SYSTEM",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ResultCode = (int)ResultCode.Success,
                    RoomInfo = new RoomInfo
                    {
                        RoomId = currentRoomId,
                        RoomName = $"채팅방 {currentRoomId}",
                        UserCount = roomUsers.Count,
                        Action = RoomAction.LeaveRoom
                    }
                };

                foreach (var user in roomUsers)
                {
                    roomUpdateMessage.RoomInfo.Users.Add(user.UserId);
                }

                await BroadcastMessageToRoom(currentRoomId, roomUpdateMessage);
            }
        }

        // 기존 ChatRoom에서도 제거 (호환성을 위해)
        if (_chatRooms.TryGetValue(currentRoomId, out var room))
        {
            room.Users.TryRemove(clientInfo.UserId, out _);
            
            // 방이 비어있으면 삭제
            if (room.Users.IsEmpty)
            {
                _chatRooms.TryRemove(currentRoomId, out _);
                _logger.LogInformation($"채팅방 {currentRoomId} 삭제됨 (사용자 없음)");
            }
        }

        clientInfo.CurrentRoomId = "";
        
        _logger.LogInformation($"사용자 {clientInfo.UserId}이(가) 채팅방을 퇴장했습니다");

        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "채팅방 퇴장 성공";
        return response;
    }

    private GameMessage ProcessRoomList(GameMessage response)
    {
        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "채팅방 목록 조회 성공";
        response.RoomInfo = new RoomInfo
        {
            Action = RoomAction.RoomList
        };

        foreach (var room in _chatRooms.Values)
        {
            response.RoomInfo.Users.Add($"{room.RoomId}:{room.RoomName}:{room.Users.Count}");
        }

        return response;
    }

    /// <summary>
    /// 🔥 핵심 메서드: 특정 방의 모든 사용자에게 메시지 브로드캐스트
    /// - 이 메서드 때문에 static 데이터가 필요합니다!
    /// - 다른 gRPC 인스턴스의 클라이언트에게도 메시지를 보낼 수 있음
    /// </summary>
    private async Task BroadcastMessageToRoom(string roomId, GameMessage message, string? excludeUserId = null)
    {
        // 1️⃣ UserManager에서 해당 방의 사용자 목록 가져오기
        var roomUsers = UserManager.Instance.GetRoomUsers(roomId);
        if (roomUsers.Count == 0)
            return;

        var tasks = new List<Task>();
        
        // 2️⃣ 각 사용자의 연결 정보 찾아서 메시지 전송 준비
        foreach (var userInfo in roomUsers)
        {
            // 제외할 사용자는 건너뛰기 (보통 메시지 보낸 본인)
            if (excludeUserId != null && userInfo.UserId == excludeUserId)
                continue;

            ClientInfo? clientInfo = null;

            // 3️⃣ 해당 사용자의 gRPC 연결 정보 찾기
            // ⭐ 여기가 핵심! static _connectedClients에서 찾기 때문에
            //    다른 gRPC 인스턴스에 연결된 클라이언트도 찾을 수 있음
            
            // 방법1: _connectedClients에서 UserId로 찾기 (주 방법)
            foreach (var kvp in _connectedClients)
            {
                if (kvp.Value.UserId == userInfo.UserId)
                {
                    clientInfo = kvp.Value;
                    break;
                }
            }

            // 방법2: 못 찾으면 _chatRooms에서도 찾기 (백업 방법)
            if (clientInfo == null)
            {
                foreach (var room in _chatRooms.Values)
                {
                    if (room.Users.TryGetValue(userInfo.UserId, out clientInfo))
                        break;
                }
            }

            // 4️⃣ 연결 정보가 있으면 메시지 전송 태스크 생성
            if (clientInfo?.ResponseStream != null)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // 🚀 실제 메시지 전송! (다른 인스턴스의 클라이언트에게도 전송됨)
                        await clientInfo.ResponseStream.WriteAsync(message);
                        _logger.LogInformation($"[BROADCAST] {userInfo.UserId}에게 메시지 전송: {message.MessageTypeCase}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"클라이언트 {userInfo.UserId}에게 메시지 전송 실패");
                    }
                }));
            }
            else
            {
                _logger.LogWarning($"[BROADCAST] {userInfo.UserId}의 ResponseStream을 찾을 수 없음 (connectedClients: {_connectedClients.Count}, chatRooms: {_chatRooms.Count})");
            }
        }

        _logger.LogInformation($"[BROADCAST] 방 {roomId}에 {tasks.Count}/{roomUsers.Count}개 메시지 전송 시도");
        
        // 5️⃣ 모든 메시지를 병렬로 전송
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);  // 모든 클라이언트에게 동시에 전송
        }
    }
}

/// <summary>
/// 클라이언트 연결 정보를 저장하는 클래스
/// - 각 클라이언트의 상태와 연결 정보를 관리
/// </summary>
public class ClientInfo
{
    public string ClientId { get; set; } = "";        // gRPC 연결 고유 ID
    public string UserId { get; set; } = "";          // 사용자 ID (로그인 후 설정)
    public IServerStreamWriter<GameMessage>? ResponseStream { get; set; }  // 🔥 핵심: 클라이언트에게 메시지 보낼 때 사용
    public bool IsAuthenticated { get; set; }         // 인증 완료 여부
    public string PassKey { get; set; } = "";         // 인증 키
    public string SubPassKey { get; set; } = "";      // 보조 인증 키
    public DateTime ConnectedAt { get; set; }         // 연결 시간
    public string CurrentRoomId { get; set; } = "";   // 현재 참여 중인 방 ID
}

/// <summary>
/// 채팅방 정보를 저장하는 클래스
/// - 방별 사용자 목록과 정보를 관리
/// </summary>
public class ChatRoom
{
    public string RoomId { get; set; } = "";          // 방 ID
    public string RoomName { get; set; } = "";        // 방 이름
    public ConcurrentDictionary<string, ClientInfo> Users { get; set; } = new();  // 방에 참여중인 사용자들
    public DateTime CreatedAt { get; set; }           // 방 생성 시간
    public DateTime LastMessageAt { get; set; }       // 마지막 메시지 시간
}