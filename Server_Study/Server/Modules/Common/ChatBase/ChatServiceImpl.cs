using DummyClient.gRPC;
using Grpc.Core;
using System.Collections.Concurrent;

namespace Server_Study.Modules.Common.ChatBase;

public class ChatServiceImpl : ChatService.ChatServiceBase
{
    private static readonly ConcurrentDictionary<string, HashSet<IServerStreamWriter<ChatMessage>>> _roomClients = new();
    private static readonly ConcurrentDictionary<string, string> _clientRooms = new();
    
    public override async Task StreamChat(
        IAsyncStreamReader<ChatMessage> requestStream,
        IServerStreamWriter<ChatMessage> responseStream,
        ServerCallContext context)
    {
        var clientId = context.Peer;
        Console.WriteLine($"[gRPC Chat] 클라이언트 연결: {clientId}");
        
        try
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                await ProcessMessage(message, responseStream, clientId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[gRPC Chat] 클라이언트 {clientId} 오류: {ex.Message}");
        }
        finally
        {
            await RemoveClientFromRoom(clientId);
            Console.WriteLine($"[gRPC Chat] 클라이언트 연결 해제: {clientId}");
        }
    }
    
    private async Task ProcessMessage(ChatMessage message, IServerStreamWriter<ChatMessage> responseStream, string clientId)
    {
        switch (message.Type)
        {
            case MessageType.Join:
                await HandleJoinRoom(message, responseStream, clientId);
                break;
                
            case MessageType.Leave:
                await HandleLeaveRoom(message, responseStream, clientId);
                break;
                
            case MessageType.Chat:
                await HandleChatMessage(message, clientId);
                break;
        }
    }
    
    private async Task HandleJoinRoom(ChatMessage message, IServerStreamWriter<ChatMessage> responseStream, string clientId)
    {
        var roomId = message.RoomId;
        var userId = message.UserId;
        
        // 기존 방에서 제거
        await RemoveClientFromRoom(clientId);
        
        // 새 방에 추가
        _roomClients.AddOrUpdate(roomId, 
            new HashSet<IServerStreamWriter<ChatMessage>> { responseStream },
            (key, existing) => { existing.Add(responseStream); return existing; });
        
        _clientRooms[clientId] = roomId;
        
        Console.WriteLine($"[gRPC Chat] {userId}님이 {roomId} 방에 입장");
        
        // 입장 메시지를 방의 모든 클라이언트에게 브로드캐스트
        var joinMessage = new ChatMessage
        {
            UserId = userId,
            RoomId = roomId,
            Content = $"{userId}님이 입장했습니다",
            Type = MessageType.Join,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        
        await BroadcastToRoom(roomId, joinMessage);
    }
    
    private async Task HandleLeaveRoom(ChatMessage message, IServerStreamWriter<ChatMessage> responseStream, string clientId)
    {
        var roomId = message.RoomId;
        var userId = message.UserId;
        
        Console.WriteLine($"[gRPC Chat] {userId}님이 {roomId} 방에서 퇴장");
        
        // 퇴장 메시지를 방의 모든 클라이언트에게 브로드캐스트
        var leaveMessage = new ChatMessage
        {
            UserId = userId,
            RoomId = roomId,
            Content = $"{userId}님이 퇴장했습니다",
            Type = MessageType.Leave,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        
        await BroadcastToRoom(roomId, leaveMessage);
        await RemoveClientFromRoom(clientId);
    }
    
    private async Task HandleChatMessage(ChatMessage message, string clientId)
    {
        if (!_clientRooms.TryGetValue(clientId, out var roomId))
        {
            Console.WriteLine($"[gRPC Chat] 클라이언트 {clientId}가 방에 입장하지 않음");
            return;
        }
        
        Console.WriteLine($"[gRPC Chat] [{roomId}] {message.UserId}: {message.Content}");
        
        // 채팅 메시지를 방의 모든 클라이언트에게 브로드캐스트
        await BroadcastToRoom(roomId, message);
    }
    
    private async Task BroadcastToRoom(string roomId, ChatMessage message)
    {
        if (!_roomClients.TryGetValue(roomId, out var clients))
            return;
            
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
        
        if (clients.Count == 0)
        {
            _roomClients.TryRemove(roomId, out _);
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
}