using GamePackets;
using Google.Protobuf;
using ServerCore;

namespace Server_Study.Modules.Common.ChatBase;

/// <summary>
/// 모든 채팅 서비스의 공통 기능을 제공하는 베이스 클래스
/// </summary>
public abstract class BaseChatService
{
    protected readonly JobQueue _jobQueue = new JobQueue();

    /// <summary>
    /// 채팅 메시지 유효성 검사
    /// </summary>
    protected virtual bool ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (message.Length > 200) // 최대 200자
            return false;

        return true;
    }

    /// <summary>
    /// 금지어 필터링
    /// </summary>
    protected virtual string FilterMessage(string message)
    {
        // 간단한 금지어 필터링 예시
        string[] badWords = { "바보", "멍청", "욕설" };
        
        foreach (var badWord in badWords)
        {
            message = message.Replace(badWord, new string('*', badWord.Length));
        }

        return message;
    }

    /// <summary>
    /// 채팅 패킷 생성
    /// </summary>
    protected S_Chat CreateChatPacket(int playerId, string message)
    {
        S_Chat packet = new S_Chat();
        packet.Playerid = playerId;
        packet.Mesage = FilterMessage(message);
        return packet;
    }

    /// <summary>
    /// 안전한 메시지 전송 (예외 처리 포함)
    /// </summary>
    protected void SafeSend(ClientSession session, ArraySegment<byte> data)
    {
        try
        {
            session.Send(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BaseChatService] 메시지 전송 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 여러 클라이언트에게 안전한 브로드캐스트
    /// </summary>
    protected void SafeBroadcast(List<ClientSession> sessions, ArraySegment<byte> data, string logContext = "")
    {
        int successCount = 0;
        int failCount = 0;

        foreach (var session in sessions)
        {
            try
            {
                session.Send(data);
                successCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                Console.WriteLine($"[BaseChatService] {logContext} 전송 실패: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(logContext))
        {
            Console.WriteLine($"[BaseChatService] {logContext} - 성공: {successCount}, 실패: {failCount}");
        }
    }

    /// <summary>
    /// 현재 시간 타임스탬프 생성
    /// </summary>
    protected long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}