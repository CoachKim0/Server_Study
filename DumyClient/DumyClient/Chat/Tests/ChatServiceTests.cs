using DummyClient.Chat.Common;
using DummyClient.Chat.Interfaces;

namespace DummyClient.Chat.Tests;

/// <summary>
/// 채팅 서비스 테스트 클래스
/// TCP와 gRPC 채팅 서비스의 기능 테스트
/// </summary>
public static class ChatServiceTests
{
    /// <summary>
    /// 기본 채팅 기능 테스트
    /// </summary>
    public static async Task BasicChatTest(ChatServiceFactory.ProtocolType protocolType)
    {
        Console.WriteLine($"\n=== {protocolType} 기본 채팅 테스트 시작 ===");
        
        var chatService = ChatServiceFactory.CreateChatService(protocolType);
        
        // 이벤트 구독
        chatService.OnMessageReceived += (args) => 
        {
            Console.WriteLine($"💬 [{args.RoomId}] {args.UserName}: {args.Message}");
        };
        
        chatService.OnUserJoined += (args) => 
        {
            Console.WriteLine($"👤 {args.Message}");
        };
        
        chatService.OnUserLeft += (args) => 
        {
            Console.WriteLine($"🚪 {args.Message}");
        };
        
        chatService.OnDisconnected += (reason) => 
        {
            Console.WriteLine($"🔌 연결 종료: {reason}");
        };

        try
        {
            // 1. 서버 연결 - 프로토콜별 포트 사용
            var port = protocolType == ChatServiceFactory.ProtocolType.TCP ? 7777 : 5554;
            var connected = await chatService.ConnectAsync("localhost", port);
            if (!connected)
            {
                Console.WriteLine("❌ 서버 연결 실패");
                return;
            }

            // 2. 방 입장
            var joined = await chatService.JoinRoomAsync("TestRoom", "TestUser");
            if (!joined)
            {
                Console.WriteLine("❌ 방 입장 실패");
                return;
            }

            // 3. 메시지 전송
            await chatService.SendMessageAsync("안녕하세요!");
            await chatService.SendMessageAsync("채팅 테스트 중입니다.");
            
            await Task.Delay(1000);
            
            // 4. 방 나가기
            await chatService.LeaveRoomAsync();
            
            // 5. 연결 해제
            await chatService.DisconnectAsync();
            
            Console.WriteLine($"✅ {protocolType} 기본 채팅 테스트 완료");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 테스트 실패: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 대화형 채팅 테스트
    /// </summary>
    public static async Task InteractiveChatTest(ChatServiceFactory.ProtocolType protocolType)
    {
        Console.WriteLine($"\n=== {protocolType} 대화형 채팅 테스트 시작 ===");
        Console.WriteLine("명령어: /quit - 종료, /leave - 방 나가기, /join [방이름] - 방 입장");
        
        var chatService = ChatServiceFactory.CreateChatService(protocolType);
        
        // 이벤트 구독
        chatService.OnMessageReceived += (args) => 
        {
            Console.WriteLine($"💬 [{args.RoomId}] {args.UserName}: {args.Message} ({args.Timestamp:HH:mm:ss})");
        };
        
        chatService.OnUserJoined += (args) => 
        {
            Console.WriteLine($"👤 {args.Message} ({args.Timestamp:HH:mm:ss})");
        };
        
        chatService.OnUserLeft += (args) => 
        {
            Console.WriteLine($"🚪 {args.Message} ({args.Timestamp:HH:mm:ss})");
        };
        
        chatService.OnDisconnected += (reason) => 
        {
            Console.WriteLine($"🔌 연결 종료: {reason}");
        };

        try
        {
            // 서버 연결
            Console.Write("서버 주소 (기본값: localhost): ");
            var serverAddress = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(serverAddress))
                serverAddress = "localhost";
                
            var defaultPort = protocolType == ChatServiceFactory.ProtocolType.TCP ? 7777 : 5554;
            Console.Write($"포트 번호 (기본값: {defaultPort}): ");
            var portInput = Console.ReadLine();
            var port = string.IsNullOrWhiteSpace(portInput) ? defaultPort : int.Parse(portInput);
            
            var connected = await chatService.ConnectAsync(serverAddress, port);
            if (!connected)
            {
                Console.WriteLine("❌ 서버 연결 실패");
                return;
            }
            
            Console.Write("사용자 이름: ");
            var userName = Console.ReadLine() ?? "Anonymous";
            
            Console.Write("방 이름: ");
            var roomName = Console.ReadLine() ?? "DefaultRoom";
            
            var joined = await chatService.JoinRoomAsync(roomName, userName);
            if (!joined)
            {
                Console.WriteLine("❌ 방 입장 실패");
                return;
            }
            
            Console.WriteLine($"✅ {roomName} 방에 입장했습니다. 채팅을 시작하세요!");
            
            // 대화형 채팅 루프
            while (true)
            {
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;
                
                if (input == "/quit")
                {
                    break;
                }
                else if (input == "/leave")
                {
                    await chatService.LeaveRoomAsync();
                    Console.WriteLine("방을 나갔습니다. 새로운 방에 입장하려면 /join [방이름]을 사용하세요.");
                }
                else if (input.StartsWith("/join "))
                {
                    var newRoomName = input.Substring(6).Trim();
                    if (!string.IsNullOrWhiteSpace(newRoomName))
                    {
                        if (!string.IsNullOrEmpty(chatService.CurrentRoom))
                        {
                            await chatService.LeaveRoomAsync();
                        }
                        await chatService.JoinRoomAsync(newRoomName, userName);
                    }
                }
                else
                {
                    await chatService.SendMessageAsync(input);
                }
            }
            
            await chatService.DisconnectAsync();
            Console.WriteLine($"✅ {protocolType} 대화형 채팅 테스트 종료");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 테스트 실패: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 모든 채팅 테스트 실행
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== 채팅 서비스 테스트 시작 ===\n");
        
        // TCP 기본 테스트
        await BasicChatTest(ChatServiceFactory.ProtocolType.TCP);
        await Task.Delay(1000);
        
        // gRPC 기본 테스트  
        await BasicChatTest(ChatServiceFactory.ProtocolType.gRPC);
        await Task.Delay(1000);
        
        Console.WriteLine("\n=== 모든 기본 테스트 완료 ===");
        Console.WriteLine("대화형 테스트를 원하시면 InteractiveChatTest를 실행하세요.");
    }
}