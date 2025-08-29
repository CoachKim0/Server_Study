using DummyClient.Chat.gRPC;
using DummyClient.Chat.Common;

namespace DummyClient;

public class TestGrpcChat
{
    public static async Task RunTest()
    {
        Console.WriteLine("=== gRPC 채팅 브로드캐스트 테스트 시작 ===");
        
        var grpcChat1 = new GrpcChatService();
        var grpcChat2 = new GrpcChatService();
        var grpcChat3 = new GrpcChatService();
        
        // 이벤트 핸들러 설정
        grpcChat1.OnMessageReceived += (args) => {
            Console.WriteLine($"[Client1 받음] {args.UserName}: {args.Message}");
        };
        
        grpcChat1.OnUserJoined += (args) => {
            Console.WriteLine($"[Client1] 입장 알림: {args.Message}");
        };
        
        grpcChat1.OnUserLeft += (args) => {
            Console.WriteLine($"[Client1] 퇴장 알림: {args.Message}");
        };
        
        grpcChat2.OnMessageReceived += (args) => {
            Console.WriteLine($"[Client2 받음] {args.UserName}: {args.Message}");
        };
        
        grpcChat2.OnUserJoined += (args) => {
            Console.WriteLine($"[Client2] 입장 알림: {args.Message}");
        };
        
        grpcChat2.OnUserLeft += (args) => {
            Console.WriteLine($"[Client2] 퇴장 알림: {args.Message}");
        };
        
        grpcChat3.OnMessageReceived += (args) => {
            Console.WriteLine($"[Client3 받음] {args.UserName}: {args.Message}");
        };
        
        grpcChat3.OnUserJoined += (args) => {
            Console.WriteLine($"[Client3] 입장 알림: {args.Message}");
        };
        
        grpcChat3.OnUserLeft += (args) => {
            Console.WriteLine($"[Client3] 퇴장 알림: {args.Message}");
        };
        
        try
        {
            // 서버 연결
            Console.WriteLine("1. 서버 연결 중...");
            bool connected1 = await grpcChat1.ConnectAsync("127.0.0.1", 5554);
            bool connected2 = await grpcChat2.ConnectAsync("127.0.0.1", 5554);
            bool connected3 = await grpcChat3.ConnectAsync("127.0.0.1", 5554);
            
            if (!connected1 || !connected2 || !connected3)
            {
                Console.WriteLine("❌ 서버 연결 실패");
                return;
            }
            
            await Task.Delay(1000);
            
            // 방 입장
            Console.WriteLine("\n2. 방 입장...");
            await grpcChat1.JoinRoomAsync("test_room", "Alice");
            await Task.Delay(500);
            
            await grpcChat2.JoinRoomAsync("test_room", "Bob");
            await Task.Delay(500);
            
            await grpcChat3.JoinRoomAsync("test_room", "Charlie");
            await Task.Delay(1000);
            
            // 브로드캐스트 채팅 메시지 전송 테스트
            Console.WriteLine("\n3. 브로드캐스트 채팅 테스트...");
            
            // Alice가 메시지 전송 (Bob, Charlie가 받아야 함)
            Console.WriteLine("\n--- Alice 메시지 전송 ---");
            await grpcChat1.SendMessageAsync("안녕하세요! 저는 Alice입니다.");
            await Task.Delay(1000);
            
            // Bob이 메시지 전송 (Alice, Charlie가 받아야 함)
            Console.WriteLine("\n--- Bob 메시지 전송 ---");
            await grpcChat2.SendMessageAsync("안녕하세요! Bob입니다. gRPC 채팅 테스트중이에요!");
            await Task.Delay(1000);
            
            // Charlie가 메시지 전송 (Alice, Bob이 받아야 함)
            Console.WriteLine("\n--- Charlie 메시지 전송 ---");
            await grpcChat3.SendMessageAsync("Charlie입니다. 브로드캐스트가 잘 되는지 확인해볼게요!");
            await Task.Delay(1000);
            
            // 연속 메시지 테스트
            Console.WriteLine("\n4. 연속 메시지 브로드캐스트 테스트...");
            await grpcChat1.SendMessageAsync("첫 번째 메시지");
            await grpcChat2.SendMessageAsync("두 번째 메시지");
            await grpcChat3.SendMessageAsync("세 번째 메시지");
            await grpcChat1.SendMessageAsync("네 번째 메시지");
            await Task.Delay(2000);
            
            // 사용자 퇴장 테스트
            Console.WriteLine("\n5. 사용자 퇴장 테스트...");
            Console.WriteLine("Charlie가 퇴장합니다...");
            await grpcChat3.LeaveRoomAsync();
            await Task.Delay(1000);
            
            // 퇴장 후 메시지 전송 테스트
            Console.WriteLine("\n6. 퇴장 후 메시지 전송 테스트...");
            await grpcChat1.SendMessageAsync("Charlie가 나간 후 Alice의 메시지");
            await grpcChat2.SendMessageAsync("Charlie가 나간 후 Bob의 메시지");
            await Task.Delay(2000);
            
            Console.WriteLine("\n7. 테스트 완료. 연결 해제...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 테스트 오류: {ex.Message}");
            Console.WriteLine($"상세 오류: {ex}");
        }
        finally
        {
            await grpcChat1.DisconnectAsync();
            await grpcChat2.DisconnectAsync();
            await grpcChat3.DisconnectAsync();
        }
        
        Console.WriteLine("=== gRPC 채팅 브로드캐스트 테스트 종료 ===");
    }
    
    public static async Task RunBroadcastStressTest()
    {
        Console.WriteLine("=== gRPC 브로드캐스트 스트레스 테스트 시작 ===");
        
        const int clientCount = 5;
        const int messageCount = 3;
        
        var clients = new List<GrpcChatService>();
        var userNames = new[] { "User1", "User2", "User3", "User4", "User5" };
        
        // 클라이언트 생성 및 이벤트 핸들러 설정
        for (int i = 0; i < clientCount; i++)
        {
            var client = new GrpcChatService();
            int clientIndex = i;
            
            client.OnMessageReceived += (args) => {
                Console.WriteLine($"[{userNames[clientIndex]} 받음] {args.UserName}: {args.Message}");
            };
            
            clients.Add(client);
        }
        
        try
        {
            // 모든 클라이언트 연결
            Console.WriteLine($"1. {clientCount}개 클라이언트 서버 연결 중...");
            var connectTasks = clients.Select(client => client.ConnectAsync("127.0.0.1", 5554)).ToArray();
            var connectResults = await Task.WhenAll(connectTasks);
            
            if (connectResults.Any(result => !result))
            {
                Console.WriteLine("❌ 일부 클라이언트 연결 실패");
                return;
            }
            
            await Task.Delay(1000);
            
            // 모든 클라이언트 방 입장
            Console.WriteLine("2. 모든 클라이언트 방 입장...");
            var joinTasks = clients.Select((client, index) => 
                client.JoinRoomAsync("stress_test_room", userNames[index])).ToArray();
            await Task.WhenAll(joinTasks);
            
            await Task.Delay(2000);
            
            // 브로드캐스트 메시지 전송 테스트
            Console.WriteLine($"3. 각 클라이언트가 {messageCount}개씩 메시지 전송...");
            
            for (int round = 1; round <= messageCount; round++)
            {
                Console.WriteLine($"\n--- Round {round} ---");
                var sendTasks = clients.Select((client, index) => 
                    client.SendMessageAsync($"{userNames[index]}의 {round}번째 메시지")).ToArray();
                
                await Task.WhenAll(sendTasks);
                await Task.Delay(1500);
            }
            
            Console.WriteLine("\n4. 스트레스 테스트 완료");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 스트레스 테스트 오류: {ex.Message}");
        }
        finally
        {
            // 모든 클라이언트 연결 해제
            var disconnectTasks = clients.Select(client => client.DisconnectAsync()).ToArray();
            await Task.WhenAll(disconnectTasks);
        }
        
        Console.WriteLine("=== gRPC 브로드캐스트 스트레스 테스트 종료 ===");
    }
}