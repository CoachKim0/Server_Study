using DummyClient.gRPC;
using Grpc.Core;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Server_Study.Modules.Common.ChatBase;

public class ChatServiceImpl : ChatService.ChatServiceBase
{
    private readonly ILogger<ChatServiceImpl> _logger;
    private static readonly ConcurrentDictionary<string, HashSet<IServerStreamWriter<ChatMessage>>> _roomClients = new();
    private static readonly ConcurrentDictionary<string, HashSet<IServerStreamWriter<ChatMessage>>> _lobbyClients = new();
    private static readonly ConcurrentDictionary<string, string> _clientRooms = new();
    
    public ChatServiceImpl(ILogger<ChatServiceImpl> logger)
    {
        _logger = logger;
    }
    
    public override async Task StreamChat(
        IAsyncStreamReader<ChatMessage> requestStream,
        IServerStreamWriter<ChatMessage> responseStream,
        ServerCallContext context)
    {
        var clientId = context.Peer;
        _logger.LogInformation($"[ChatService] 클라이언트 연결: {clientId}");
        
        try
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                await ProcessMessage(message, responseStream, clientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[ChatService] 클라이언트 {clientId} 오류");
        }
        finally
        {
            await RemoveClientFromAll(clientId, responseStream);
            _logger.LogInformation($"[ChatService] 클라이언트 연결 해제: {clientId}");
        }
    }
    
    private async Task ProcessMessage(ChatMessage message, IServerStreamWriter<ChatMessage> responseStream, string clientId)
    {
        // 컨텍스트별 메시지 처리
        switch (message.ChatContextCase)
        {
            case ChatMessage.ChatContextOneofCase.LobbyChat:
                await HandleLobbyChat(message, responseStream, clientId);
                break;
                
            case ChatMessage.ChatContextOneofCase.RoomChat:
                await HandleRoomChat(message, responseStream, clientId);
                break;
                
            case ChatMessage.ChatContextOneofCase.TestChat:
                await HandleTestChat(message, responseStream, clientId);
                break;
                
            default:
                _logger.LogWarning($"[ChatService] 알 수 없는 채팅 컨텍스트: {message.ChatContextCase}");
                break;
        }
    }
    
    // 로비 채팅 처리
    private async Task HandleLobbyChat(ChatMessage message, IServerStreamWriter<ChatMessage> responseStream, string clientId)
    {
        var lobbyChat = message.LobbyChat;
        
        // 로비 클라이언트 목록에 추가 (채팅 발송을 위해)
        _lobbyClients.AddOrUpdate("lobby", 
            new HashSet<IServerStreamWriter<ChatMessage>> { responseStream },
            (key, existing) => { existing.Add(responseStream); return existing; });
        
        _logger.LogInformation($"[LobbyChat] {message.UserId}: {lobbyChat.Content}");
        
        var responseMessage = new ChatMessage
        {
            UserId = message.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LobbyChat = new LobbyChatMessage
            {
                Content = lobbyChat.Content,
                Type = lobbyChat.Type,
                AnnouncementLevel = lobbyChat.AnnouncementLevel
            }
        };
        
        await BroadcastToLobby(responseMessage);
    }
    
    // 룸 채팅 처리
    private async Task HandleRoomChat(ChatMessage message, IServerStreamWriter<ChatMessage> responseStream, string clientId)
    {
        var roomChat = message.RoomChat;
        
        // 룸 입장/퇴장 처리
        if (roomChat.Type == RoomMessageType.RoomJoin)
        {
            await JoinRoom(roomChat.RoomId, responseStream, clientId, message.UserId);
            return;
        }
        else if (roomChat.Type == RoomMessageType.RoomLeave)
        {
            await LeaveRoom(roomChat.RoomId, clientId, message.UserId);
            return;
        }
        
        _logger.LogInformation($"[RoomChat] [{roomChat.RoomId}] {message.UserId}: {roomChat.Content}");
        
        var responseMessage = new ChatMessage
        {
            UserId = message.UserId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            RoomChat = new RoomChatMessage
            {
                RoomId = roomChat.RoomId,
                Content = roomChat.Content,
                Type = roomChat.Type,
                TeamId = roomChat.TeamId
            }
        };
        
        await BroadcastToRoom(roomChat.RoomId, responseMessage);
    }
    
    // 테스트 채팅 처리 (기존 호환용)
    private async Task HandleTestChat(ChatMessage message, IServerStreamWriter<ChatMessage> responseStream, string clientId)
    {
        var testChat = message.TestChat;
        _logger.LogInformation($"[TestChat] [{testChat.RoomId}] {message.UserId}: {testChat.Content}");
        
        // 기존 로직과 호환성 유지
        switch (testChat.Type)
        {
            case MessageType.Join:
                await JoinRoom(testChat.RoomId, responseStream, clientId, message.UserId);
                
                // 입장 메시지 생성
                var joinMessage = new ChatMessage
                {
                    UserId = message.UserId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    TestChat = new TestChatMessage
                    {
                        RoomId = testChat.RoomId,
                        Content = $"{message.UserId}님이 입장했습니다",
                        Type = MessageType.Join
                    }
                };
                await BroadcastToRoom(testChat.RoomId, joinMessage);
                break;
                
            case MessageType.Leave:
                // 퇴장 메시지 생성
                var leaveMessage = new ChatMessage
                {
                    UserId = message.UserId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    TestChat = new TestChatMessage
                    {
                        RoomId = testChat.RoomId,
                        Content = $"{message.UserId}님이 퇴장했습니다",
                        Type = MessageType.Leave
                    }
                };
                await BroadcastToRoom(testChat.RoomId, leaveMessage);
                await LeaveRoom(testChat.RoomId, clientId, message.UserId);
                break;
                
            case MessageType.Chat:
                // 채팅 메시지 브로드캐스트
                var chatMessage = new ChatMessage
                {
                    UserId = message.UserId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    TestChat = new TestChatMessage
                    {
                        RoomId = testChat.RoomId,
                        Content = testChat.Content,
                        Type = MessageType.Chat
                    }
                };
                await BroadcastToRoom(testChat.RoomId, chatMessage);
                break;
        }
    }
    
    private async Task JoinRoom(string roomId, IServerStreamWriter<ChatMessage> responseStream, string clientId, string userId)
    {
        // 기존 방에서 제거
        await RemoveClientFromRoom(clientId);
        
        // 새 방에 추가
        _roomClients.AddOrUpdate(roomId, 
            new HashSet<IServerStreamWriter<ChatMessage>> { responseStream },
            (key, existing) => { existing.Add(responseStream); return existing; });
        
        _clientRooms[clientId] = roomId;
        _logger.LogInformation($"[ChatService] {userId}님이 {roomId} 방에 입장");
    }
    
    private async Task LeaveRoom(string roomId, string clientId, string userId)
    {
        await RemoveClientFromRoom(clientId);
        _logger.LogInformation($"[ChatService] {userId}님이 {roomId} 방에서 퇴장");
    }
    
    private async Task BroadcastToLobby(ChatMessage message)
    {
        if (_lobbyClients.TryGetValue("lobby", out var clients))
        {
            await BroadcastToClients(clients, message);
        }
    }
    
    private async Task BroadcastToRoom(string roomId, ChatMessage message)
    {
        if (_roomClients.TryGetValue(roomId, out var clients))
        {
            await BroadcastToClients(clients, message);
        }
    }
    
    private async Task BroadcastToClients(HashSet<IServerStreamWriter<ChatMessage>> clients, ChatMessage message)
    {
        var tasks = new List<Task>();
        var clientsToRemove = new List<IServerStreamWriter<ChatMessage>>();
        
        foreach (var client in clients.ToList())
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await client.WriteAsync(message);
                }
                catch
                {
                    lock (clientsToRemove)
                    {
                        clientsToRemove.Add(client);
                    }
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // 연결이 끊어진 클라이언트 제거
        foreach (var client in clientsToRemove)
        {
            clients.Remove(client);
        }
    }
    
    private async Task RemoveClientFromRoom(string clientId)
    {
        if (_clientRooms.TryRemove(clientId, out var roomId))
        {
            if (_roomClients.TryGetValue(roomId, out var clients))
            {
                var clientToRemove = clients.FirstOrDefault();
                if (clientToRemove != null)
                {
                    clients.Remove(clientToRemove);
                    if (clients.Count == 0)
                    {
                        _roomClients.TryRemove(roomId, out _);
                    }
                }
            }
        }
        await Task.CompletedTask;
    }
    
    private async Task RemoveClientFromAll(string clientId, IServerStreamWriter<ChatMessage> responseStream)
    {
        // 룸에서 제거
        await RemoveClientFromRoom(clientId);
        
        // 로비에서 제거
        if (_lobbyClients.TryGetValue("lobby", out var lobbyClients))
        {
            lobbyClients.Remove(responseStream);
            if (lobbyClients.Count == 0)
            {
                _lobbyClients.TryRemove("lobby", out _);
            }
        }
    }
}