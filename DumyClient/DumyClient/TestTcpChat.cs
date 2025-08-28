using DummyClient.Chat.TCP;
using DummyClient.Chat.Common;

namespace DummyClient;

public class TestTcpChat
{
    public static async Task RunTest()
    {
        Console.WriteLine("=== TCP 채팅 Protocol Buffers 테스트 시작 ===");
        
        var tcpChat1 = new TcpChatService();
        var tcpChat2 = new TcpChatService();
        
        // 이벤트 핸들러 설정
        tcpChat1.OnMessageReceived += (args) => {
            Console.WriteLine($"[Client1 받음] {args.UserName}: {args.Message}");
        };
        
        tcpChat2.OnMessageReceived += (args) => {
            Console.WriteLine($"[Client2 받음] {args.UserName}: {args.Message}");
        };
        
        try
        {
            // 서버 연결
            Console.WriteLine("1. 서버 연결 중...");
            bool connected1 = await tcpChat1.ConnectAsync("127.0.0.1", 7777);
            bool connected2 = await tcpChat2.ConnectAsync("127.0.0.1", 7777);
            
            if (!connected1 || !connected2)
            {
                Console.WriteLine("❌ 서버 연결 실패");
                return;
            }
            
            await Task.Delay(1000);
            
            // 방 입장
            Console.WriteLine("\n2. 방 입장...");
            await tcpChat1.JoinRoomAsync("test_room", "User1");
            await tcpChat2.JoinRoomAsync("test_room", "User2");
            
            await Task.Delay(1000);
            
            // 메시지 전송 테스트
            Console.WriteLine("\n3. 채팅 메시지 전송 테스트...");
            await tcpChat1.SendMessageAsync("안녕하세요! User1입니다.");
            await Task.Delay(500);
            
            await tcpChat2.SendMessageAsync("안녕하세요! User2입니다.");
            await Task.Delay(500);
            
            await tcpChat1.SendMessageAsync("Protocol Buffers로 채팅이 잘 되는지 테스트합니다.");
            await Task.Delay(500);
            
            await tcpChat2.SendMessageAsync("네! 잘 동작하네요!");
            await Task.Delay(2000);
            
            Console.WriteLine("\n4. 테스트 완료. 연결 해제...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 테스트 오류: {ex.Message}");
        }
        finally
        {
            await tcpChat1.DisconnectAsync();
            await tcpChat2.DisconnectAsync();
        }
        
        Console.WriteLine("=== TCP 채팅 테스트 종료 ===");
    }
}