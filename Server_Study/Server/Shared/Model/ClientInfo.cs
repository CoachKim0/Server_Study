using GrpcApp;
using Grpc.Core;

namespace Server_Study.Shared.Model;

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