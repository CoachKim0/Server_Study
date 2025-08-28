using GrpcApp;
using Server.Grpc.Services;
using Server_Study.Managers;
using Server_Study.Shared.Model;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Server_Study.Modules.Chat;

/// <summary>
/// 기본 더미 채팅 핸들러
/// - 현재 GameGrpcService에 있던 채팅 로직을 그대로 옮김
/// - 나중에 컨텍스트별 핸들러로 대체 예정
/// </summary>
public class DummyChatHandler : IChatHandler
{
    private readonly ILogger<DummyChatHandler> _logger;
    private readonly IBroadcastService _broadcastService;
    
    // 채팅방 정보 (임시로 static 유지)
    private static readonly ConcurrentDictionary<string, ChatRoom> _chatRooms = new();

    public DummyChatHandler(ILogger<DummyChatHandler> logger, IBroadcastService broadcastService)
    {
        _logger = logger;
        _broadcastService = broadcastService;
    }

    public bool CanHandle(ChatType chatType)
    {
        // 현재는 모든 채팅 타입을 처리 (더미)
        return chatType == ChatType.NormalChat;
    }

    public async Task<GameMessage> ProcessChatMessage(GameMessage request, ClientInfo clientInfo)
    {
        var response = new GameMessage
        {
            UserId = request.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

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

        _logger.LogInformation($"[DummyChat] 채팅 메시지: [{chatMessage.RoomId}] {chatMessage.Nickname}: {chatMessage.Content}");

        // 채팅방의 모든 사용자에게 메시지 브로드캐스트
        var broadcastMessage = new GameMessage
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
        };

        await _broadcastService.Broadcast(BroadcastType.ToRoom, broadcastMessage, BroadcastTarget.Room(chatMessage.RoomId));

        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "메시지 전송 성공";
        return response;
    }
}