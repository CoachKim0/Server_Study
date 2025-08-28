using GrpcApp;
using Server.Grpc.Services;
using Server_Study.Shared.Model;
using Server_Study.Services;
using Server_Study.Managers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Server_Study.Services;

/// <summary>
/// 브로드캐스트 서비스 구현
/// </summary>
public class BroadcastService : IBroadcastService
{
    private readonly ILogger<BroadcastService> _logger;
    private static readonly ConcurrentDictionary<string, ClientInfo> _connectedClients = new();

    public BroadcastService(ILogger<BroadcastService> logger)
    {
        _logger = logger;
    }

    public async Task Broadcast(BroadcastType type, GameMessage message, BroadcastTarget target)
    {
        switch (type)
        {
            case BroadcastType.ToRoom:
                await BroadcastMessageToRoom(target.RoomId, message);
                break;
                
            case BroadcastType.ToRoomExceptUser:
                await BroadcastMessageToRoom(target.RoomId, message, target.ExcludeUserId);
                break;
                
            default:
                _logger.LogWarning($"지원되지 않는 브로드캐스트 타입: {type}");
                break;
        }
    }

    private async Task BroadcastMessageToRoom(string roomId, GameMessage message, string? excludeUserId = null)
    {
        var roomUsers = UserManager.Instance.GetRoomUsers(roomId);
        
        foreach (var user in roomUsers)
        {
            if (excludeUserId != null && user.UserId == excludeUserId)
                continue;
                
            if (_connectedClients.TryGetValue(user.UserId, out var clientInfo))
            {
                try
                {
                    await clientInfo.ResponseStream.WriteAsync(message);
                    _logger.LogDebug($"브로드캐스트 메시지 전송 성공: {user.UserId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"브로드캐스트 메시지 전송 실패: {user.UserId}, Error: {ex.Message}");
                    _connectedClients.TryRemove(user.UserId, out _);
                }
            }
        }
    }

    /// <summary>
    /// 클라이언트를 등록합니다 (GameGrpcService에서 호출)
    /// </summary>
    public void RegisterClient(string clientId, ClientInfo clientInfo)
    {
        _connectedClients.TryAdd(clientId, clientInfo);
    }

    /// <summary>
    /// 클라이언트를 제거합니다 (GameGrpcService에서 호출)
    /// </summary>
    public void UnregisterClient(string clientId)
    {
        _connectedClients.TryRemove(clientId, out _);
    }
}