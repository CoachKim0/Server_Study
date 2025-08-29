using GrpcApp;
using Server.Grpc.Services;
using Server_Study.Managers;
using Server_Study.Shared.Model;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Server_Study.Modules.Room;

/// <summary>
/// 방 관리 핸들러
/// - 방 입장, 퇴장, 목록 조회 등 방 관련 로직을 처리
/// </summary>
public class RoomHandler : IRoomHandler
{
    private readonly ILogger<RoomHandler> _logger;
    
    // 채팅방 정보 (임시로 static 유지)
    private static readonly ConcurrentDictionary<string, ChatRoom> _chatRooms = new();

    public RoomHandler(ILogger<RoomHandler> logger)
    {
        _logger = logger;
    }

    public async Task<GameMessage> ProcessRoomInfo(GameMessage request, ClientInfo clientInfo, IBroadcastService broadcastService)
    {
        var response = new GameMessage
        {
            UserId = request.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var roomInfo = request.RoomInfo;

        switch (roomInfo.Action)
        {
            case RoomAction.JoinRoom:
                return await ProcessJoinRoom(roomInfo, clientInfo, response, broadcastService);

            case RoomAction.LeaveRoom:
                return await ProcessLeaveRoom(roomInfo, clientInfo, response, broadcastService);

            case RoomAction.RoomList:
                return ProcessRoomList(response);

            default:
                response.ResultCode = (int)ResultCode.Fail;
                response.ResultMessage = "지원되지 않는 룸 액션입니다";
                return response;
        }
    }

    private async Task<GameMessage> ProcessJoinRoom(RoomInfo roomInfo, ClientInfo clientInfo, GameMessage response, IBroadcastService broadcastService)
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

        _logger.LogInformation($"[RoomHandler] 사용자 {clientInfo.UserId}이(가) 채팅방 {roomInfo.RoomId}에 입장했습니다");

        // 다른 사용자들에게 입장 알림 (RoomInfo로 전송)
        await broadcastService.Broadcast(BroadcastType.ToRoomExceptUser, new GameMessage
        {
            UserId = "SYSTEM",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ResultCode = (int)ResultCode.Success,
            RoomInfo = new RoomInfo
            {
                RoomId = roomInfo.RoomId,
                RoomName = roomInfo.RoomName ?? $"채팅방 {roomInfo.RoomId}",
                Users = { room.Users.Keys },
                UserCount = room.Users.Count,
                Action = RoomAction.JoinRoom
            }
        }, BroadcastTarget.RoomExceptUser(roomInfo.RoomId, clientInfo.UserId)); // 본인 제외

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

        await broadcastService.Broadcast(BroadcastType.ToRoomExceptUser, roomUpdateMessage, BroadcastTarget.RoomExceptUser(roomInfo.RoomId, clientInfo.UserId)); // 본인 제외

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

    private async Task<GameMessage> ProcessLeaveRoom(RoomInfo roomInfo, ClientInfo clientInfo, GameMessage response, IBroadcastService broadcastService)
    {
        if (string.IsNullOrEmpty(clientInfo.CurrentRoomId))
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "참여 중인 채팅방이 없습니다";
            return response;
        }

        // UserManager를 통한 채팅방 퇴장 처리
        string currentRoomId = clientInfo.CurrentRoomId;
        var room = _chatRooms.TryGetValue(currentRoomId, out var existingRoom) ? existingRoom : null;
        bool left = UserManager.Instance.LeaveRoom(clientInfo.UserId, currentRoomId);
        
        if (left)
        {
            // 기존 ChatRoom에서도 제거
            room?.Users.TryRemove(clientInfo.UserId, out _);
            
            // 다른 사용자들에게 퇴장 알림 (RoomInfo로 전송)
            await broadcastService.Broadcast(BroadcastType.ToRoom, new GameMessage
            {
                UserId = "SYSTEM",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ResultCode = (int)ResultCode.Success,
                RoomInfo = new RoomInfo
                {
                    RoomId = currentRoomId,
                    RoomName = room?.RoomName ?? $"채팅방 {currentRoomId}",
                    Users = { room?.Users.Keys ?? new string[0] },
                    UserCount = room?.Users.Count ?? 0,
                    Action = RoomAction.LeaveRoom
                }
            }, BroadcastTarget.Room(currentRoomId));

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

                await broadcastService.Broadcast(BroadcastType.ToRoom, roomUpdateMessage, BroadcastTarget.Room(currentRoomId));
            }
        }

        // 기존 ChatRoom에서도 제거 (호환성을 위해)
        if (_chatRooms.TryGetValue(currentRoomId, out var roomToRemove))
        {
            roomToRemove.Users.TryRemove(clientInfo.UserId, out _);
            
            // 방이 비어있으면 삭제
            if (roomToRemove.Users.IsEmpty)
            {
                _chatRooms.TryRemove(currentRoomId, out _);
                _logger.LogInformation($"[RoomHandler] 채팅방 {currentRoomId} 삭제됨 (사용자 없음)");
            }
        }

        clientInfo.CurrentRoomId = "";
        
        _logger.LogInformation($"[RoomHandler] 사용자 {clientInfo.UserId}이(가) 채팅방을 퇴장했습니다");

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
}