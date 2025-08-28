using GamePackets;
using Google.Protobuf;
using ServerCore;
using Server_Study.Shared.Utils;

namespace Server_Study.Modules.Lobby.Chat;

/// <summary>
/// 로비 전용 채팅 시스템
/// - 전체 로비 참여자에게 브로드캐스트
/// - 시스템 공지사항 지원
/// - 사용자 입장/퇴장 알림
/// </summary>
public class LobbyChat
{
    private List<ClientSession> _lobbySessions = new List<ClientSession>();
    private readonly object _lock = new object();
    private readonly JobQueue _jobQueue = new JobQueue();
    private readonly UserManager _userManager = UserManager.Instance;

    public void EnterLobby(ClientSession session, string userId)
    {
        lock (_lock)
        {
            _lobbySessions.Add(session);
            _userManager.JoinRoom(userId, "lobby");
        }

        // 입장 알림 브로드캐스트
        BroadcastSystemMessage($"{userId}님이 로비에 입장했습니다.");
        Console.WriteLine($"[LobbyChat] {userId} 로비 입장 (현재 {GetLobbyUserCount()}명)");
    }

    public void LeaveLobby(ClientSession session, string userId)
    {
        lock (_lock)
        {
            _lobbySessions.Remove(session);
            _userManager.LeaveRoom(userId, "lobby");
        }

        // 퇴장 알림 브로드캐스트
        BroadcastSystemMessage($"{userId}님이 로비를 떠났습니다.");
        Console.WriteLine($"[LobbyChat] {userId} 로비 퇴장 (현재 {GetLobbyUserCount()}명)");
    }

    public void BroadcastChat(ClientSession senderSession, string userId, string message)
    {
        _jobQueue.Push(() => {
            S_Chat packet = new S_Chat();
            packet.Playerid = senderSession.SessionId;
            packet.Mesage = $"[로비] {userId}: {message}";
            ArraySegment<byte> segment = packet.ToByteArray();

            List<ClientSession> sessionsCopy;
            lock (_lock)
            {
                sessionsCopy = new List<ClientSession>(_lobbySessions);
            }

            foreach (var client in sessionsCopy)
            {
                try
                {
                    client.Send(segment);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LobbyChat] 브로드캐스트 오류: {ex.Message}");
                }
            }

            Console.WriteLine($"[LobbyChat] 채팅 전송 완료: {sessionsCopy.Count}명에게 전송");
        });
    }

    public void BroadcastSystemMessage(string message)
    {
        _jobQueue.Push(() => {
            S_Chat packet = new S_Chat();
            packet.Playerid = -1; // 시스템 메시지
            packet.Mesage = $"[시스템] {message}";
            ArraySegment<byte> segment = packet.ToByteArray();

            List<ClientSession> sessionsCopy;
            lock (_lock)
            {
                sessionsCopy = new List<ClientSession>(_lobbySessions);
            }

            foreach (var client in sessionsCopy)
            {
                try
                {
                    client.Send(segment);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LobbyChat] 시스템 메시지 전송 오류: {ex.Message}");
                }
            }
        });
    }

    public int GetLobbyUserCount()
    {
        lock (_lock)
        {
            return _lobbySessions.Count;
        }
    }
}