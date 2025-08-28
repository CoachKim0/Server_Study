using GamePackets;
using Google.Protobuf;
using ServerCore;
using Server_Study.Shared.Utils;

namespace Server_Study.Modules.OutdoorActivity.Chat;

/// <summary>
/// 야외활동 전용 채팅 시스템
/// - 위치 기반 채팅 (근처 플레이어들에게만 전송)
/// - 활동별 전용 채널
/// - 이모티콘 및 위치 정보 지원
/// </summary>
public class OutdoorChat
{
    private Dictionary<string, List<ClientSession>> _activityRooms = new Dictionary<string, List<ClientSession>>();
    private Dictionary<ClientSession, (float x, float y)> _playerPositions = new Dictionary<ClientSession, (float, float)>();
    private readonly object _lock = new object();
    private readonly JobQueue _jobQueue = new JobQueue();
    private readonly UserManager _userManager = UserManager.Instance;
    private const float CHAT_RANGE = 100.0f; // 채팅 범위 (거리 단위)

    public void JoinActivity(ClientSession session, string userId, string activityId, float posX, float posY)
    {
        lock (_lock)
        {
            if (!_activityRooms.ContainsKey(activityId))
            {
                _activityRooms[activityId] = new List<ClientSession>();
            }

            _activityRooms[activityId].Add(session);
            _playerPositions[session] = (posX, posY);
            _userManager.JoinRoom(userId, $"outdoor_{activityId}");
        }

        // 활동 참여 알림
        BroadcastToActivity(activityId, session, $"{userId}님이 {activityId} 활동에 참여했습니다.", isSystemMessage: true);
        Console.WriteLine($"[OutdoorChat] {userId} {activityId} 활동 참여 (위치: {posX}, {posY})");
    }

    public void LeaveActivity(ClientSession session, string userId, string activityId)
    {
        lock (_lock)
        {
            if (_activityRooms.ContainsKey(activityId))
            {
                _activityRooms[activityId].Remove(session);
                if (_activityRooms[activityId].Count == 0)
                {
                    _activityRooms.Remove(activityId);
                }
            }
            _playerPositions.Remove(session);
            _userManager.LeaveRoom(userId, $"outdoor_{activityId}");
        }

        BroadcastToActivity(activityId, session, $"{userId}님이 {activityId} 활동을 떠났습니다.", isSystemMessage: true);
        Console.WriteLine($"[OutdoorChat] {userId} {activityId} 활동 종료");
    }

    public void UpdatePlayerPosition(ClientSession session, float posX, float posY)
    {
        lock (_lock)
        {
            if (_playerPositions.ContainsKey(session))
            {
                _playerPositions[session] = (posX, posY);
            }
        }
    }

    public void BroadcastLocationChat(ClientSession senderSession, string userId, string activityId, string message)
    {
        _jobQueue.Push(() => {
            if (!_playerPositions.ContainsKey(senderSession))
                return;

            var senderPos = _playerPositions[senderSession];
            var nearbyPlayers = GetNearbyPlayers(senderSession, activityId);

            S_Chat packet = new S_Chat();
            packet.Playerid = senderSession.SessionId;
            packet.Mesage = $"[{activityId}] {userId}: {message}";
            ArraySegment<byte> segment = packet.ToByteArray();

            foreach (var client in nearbyPlayers)
            {
                try
                {
                    client.Send(segment);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OutdoorChat] 위치 채팅 전송 오류: {ex.Message}");
                }
            }

            Console.WriteLine($"[OutdoorChat] 위치 채팅 전송: {nearbyPlayers.Count}명에게 전송 (범위: {CHAT_RANGE}m)");
        });
    }

    public void BroadcastToActivity(string activityId, ClientSession? excludeSession, string message, bool isSystemMessage = false)
    {
        _jobQueue.Push(() => {
            List<ClientSession> sessionsCopy;
            lock (_lock)
            {
                if (!_activityRooms.ContainsKey(activityId))
                    return;

                sessionsCopy = new List<ClientSession>(_activityRooms[activityId]);
                if (excludeSession != null)
                {
                    sessionsCopy.Remove(excludeSession);
                }
            }

            S_Chat packet = new S_Chat();
            packet.Playerid = isSystemMessage ? -1 : 0;
            packet.Mesage = isSystemMessage ? $"[시스템] {message}" : message;
            ArraySegment<byte> segment = packet.ToByteArray();

            foreach (var client in sessionsCopy)
            {
                try
                {
                    client.Send(segment);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OutdoorChat] 활동 브로드캐스트 오류: {ex.Message}");
                }
            }
        });
    }

    private List<ClientSession> GetNearbyPlayers(ClientSession senderSession, string activityId)
    {
        var nearbyPlayers = new List<ClientSession>();
        
        lock (_lock)
        {
            if (!_activityRooms.ContainsKey(activityId) || !_playerPositions.ContainsKey(senderSession))
                return nearbyPlayers;

            var senderPos = _playerPositions[senderSession];

            foreach (var session in _activityRooms[activityId])
            {
                if (_playerPositions.ContainsKey(session))
                {
                    var playerPos = _playerPositions[session];
                    float distance = CalculateDistance(senderPos, playerPos);
                    
                    if (distance <= CHAT_RANGE)
                    {
                        nearbyPlayers.Add(session);
                    }
                }
            }
        }

        return nearbyPlayers;
    }

    private float CalculateDistance((float x, float y) pos1, (float x, float y) pos2)
    {
        float dx = pos1.x - pos2.x;
        float dy = pos1.y - pos2.y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public int GetActivityParticipantCount(string activityId)
    {
        lock (_lock)
        {
            return _activityRooms.ContainsKey(activityId) ? _activityRooms[activityId].Count : 0;
        }
    }
}