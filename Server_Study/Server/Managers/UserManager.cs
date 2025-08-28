using System.Collections.Concurrent;

namespace Server_Study.Managers;

/// <summary>
/// gRPC와 TCP 프로토콜에서 공통으로 사용하는 중앙 사용자 관리 시스템
/// </summary>
public class UserManager
{
    private static UserManager? _instance;
    private static readonly object _lock = new object();
    
    public static UserManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new UserManager();
                }
            }
            return _instance;
        }
    }

    // 인증된 사용자 정보 저장
    private readonly ConcurrentDictionary<string, UserInfo> _authenticatedUsers = new();
    
    // 채팅방별 참여자 관리
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, UserInfo>> _roomUsers = new();

    private UserManager() { }

    /// <summary>
    /// 사용자 인증 처리
    /// </summary>
    public bool AuthenticateUser(string userId, string? nickname = null)
    {
        var userInfo = new UserInfo
        {
            UserId = userId,
            Nickname = nickname ?? userId,
            AuthenticatedAt = DateTime.UtcNow,
            IsAuthenticated = true
        };

        bool added = _authenticatedUsers.TryAdd(userId, userInfo);
        Console.WriteLine($"[UserManager] 사용자 {userId} 인증 {(added ? "성공" : "이미 존재")}");
        return added;
    }

    /// <summary>
    /// 사용자 인증 해제
    /// </summary>
    public bool LogoutUser(string userId)
    {
        bool removed = _authenticatedUsers.TryRemove(userId, out var userInfo);
        if (removed && userInfo != null)
        {
            // 모든 채팅방에서 제거
            foreach (var room in _roomUsers.Values)
            {
                room.TryRemove(userId, out _);
            }
            Console.WriteLine($"[UserManager] 사용자 {userId} 로그아웃 완료");
        }
        return removed;
    }

    /// <summary>
    /// 사용자가 인증되었는지 확인
    /// </summary>
    public bool IsUserAuthenticated(string userId)
    {
        return _authenticatedUsers.ContainsKey(userId);
    }

    /// <summary>
    /// 인증된 사용자 정보 조회
    /// </summary>
    public UserInfo? GetUserInfo(string userId)
    {
        _authenticatedUsers.TryGetValue(userId, out var userInfo);
        return userInfo;
    }

    /// <summary>
    /// 채팅방에 사용자 입장
    /// </summary>
    public bool JoinRoom(string userId, string roomId)
    {
        if (!IsUserAuthenticated(userId))
        {
            Console.WriteLine($"[UserManager] 인증되지 않은 사용자: {userId}");
            return false;
        }

        var userInfo = GetUserInfo(userId);
        if (userInfo == null) return false;

        // 채팅방 생성 또는 기존 방 가져오기
        var roomUsers = _roomUsers.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, UserInfo>());
        
        bool added = roomUsers.TryAdd(userId, userInfo);
        if (added)
        {
            userInfo.CurrentRoomId = roomId;
            Console.WriteLine($"[UserManager] 사용자 {userId}가 방 {roomId}에 입장 (현재 참여자: {roomUsers.Count}명)");
        }
        
        return added;
    }

    /// <summary>
    /// 채팅방에서 사용자 퇴장
    /// </summary>
    public bool LeaveRoom(string userId, string roomId)
    {
        if (!_roomUsers.TryGetValue(roomId, out var roomUsers))
            return false;

        bool removed = roomUsers.TryRemove(userId, out var userInfo);
        if (removed && userInfo != null)
        {
            userInfo.CurrentRoomId = null;
            Console.WriteLine($"[UserManager] 사용자 {userId}가 방 {roomId}에서 퇴장 (현재 참여자: {roomUsers.Count}명)");
            
            // 방이 비었으면 제거
            if (roomUsers.IsEmpty)
            {
                _roomUsers.TryRemove(roomId, out _);
                Console.WriteLine($"[UserManager] 빈 방 {roomId} 제거");
            }
        }

        return removed;
    }

    /// <summary>
    /// 채팅방 참여자 목록 조회
    /// </summary>
    public List<UserInfo> GetRoomUsers(string roomId)
    {
        if (!_roomUsers.TryGetValue(roomId, out var roomUsers))
            return new List<UserInfo>();

        return roomUsers.Values.ToList();
    }

    /// <summary>
    /// 채팅방 참여자 수 조회
    /// </summary>
    public int GetRoomUserCount(string roomId)
    {
        if (!_roomUsers.TryGetValue(roomId, out var roomUsers))
            return 0;

        return roomUsers.Count;
    }

    /// <summary>
    /// 사용자가 현재 참여 중인 방 ID 조회
    /// </summary>
    public string? GetUserCurrentRoom(string userId)
    {
        return GetUserInfo(userId)?.CurrentRoomId;
    }

    /// <summary>
    /// 전체 인증된 사용자 수 조회
    /// </summary>
    public int GetAuthenticatedUserCount()
    {
        return _authenticatedUsers.Count;
    }

    /// <summary>
    /// 전체 활성 채팅방 수 조회
    /// </summary>
    public int GetActiveRoomCount()
    {
        return _roomUsers.Count;
    }
}

/// <summary>
/// 사용자 정보 클래스
/// </summary>
public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public DateTime AuthenticatedAt { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? CurrentRoomId { get; set; }
    
    // TCP 세션 연결 정보 (필요시 사용)
    public ClientSession? TcpSession { get; set; }
}