using DummyClient.Core.Interfaces;
using DummyClient.Core.NetworkManagers;

namespace DummyClient.Tests;

/// <summary>
/// 채팅 매니저 테스트
/// </summary>
public static class ChatManagerTests
{
    public static async Task TestTcpChatManager()
    {
        Console.WriteLine("=== TCP Chat Manager Test ===");
        
        var tcpChatManager = NetworkManagerFactory.CreateChatManager(NetworkManagerFactory.NetworkType.TCP);
        
        // 이벤트 구독
        tcpChatManager.OnChatReceived += (message) => 
            Console.WriteLine($"[TCP 채팅 수신] {message}");
        tcpChatManager.OnUserJoined += (user) => 
            Console.WriteLine($"[TCP 사용자 입장] {user}님이 입장했습니다");
        tcpChatManager.OnUserLeft += (user) => 
            Console.WriteLine($"[TCP 사용자 퇴장] {user}님이 퇴장했습니다");
        
        Console.WriteLine($"현재 방: {tcpChatManager.CurrentRoom}");
        
        // 방 입장 테스트
        bool joinResult = await tcpChatManager.JoinRoomAsync("TestRoom", "User1");
        Console.WriteLine($"방 입장 결과: {joinResult}");
        Console.WriteLine($"입장 후 현재 방: {tcpChatManager.CurrentRoom}");
        
        // 메시지 전송 테스트
        await tcpChatManager.SendMessageAsync("TCP에서 보내는 테스트 메시지!");
        
        // 방 나가기 테스트
        bool leaveResult = await tcpChatManager.LeaveRoomAsync();
        Console.WriteLine($"방 나가기 결과: {leaveResult}");
        Console.WriteLine($"퇴장 후 현재 방: {tcpChatManager.CurrentRoom}");
    }
    
    public static async Task TestGrpcChatManager()
    {
        Console.WriteLine("=== gRPC Chat Manager Test ===");
        
        var grpcChatManager = NetworkManagerFactory.CreateChatManager(NetworkManagerFactory.NetworkType.gRPC);
        
        // 이벤트 구독
        grpcChatManager.OnChatReceived += (message) => 
            Console.WriteLine($"[gRPC 채팅 수신] {message}");
        grpcChatManager.OnUserJoined += (user) => 
            Console.WriteLine($"[gRPC 사용자 입장] {user}님이 입장했습니다");
        grpcChatManager.OnUserLeft += (user) => 
            Console.WriteLine($"[gRPC 사용자 퇴장] {user}님이 퇴장했습니다");
        
        Console.WriteLine($"현재 방: {grpcChatManager.CurrentRoom}");
        
        // 방 입장 테스트
        bool joinResult = await grpcChatManager.JoinRoomAsync("TestRoom", "User2");
        Console.WriteLine($"방 입장 결과: {joinResult}");
        Console.WriteLine($"입장 후 현재 방: {grpcChatManager.CurrentRoom}");
        
        // 메시지 전송 테스트
        await grpcChatManager.SendMessageAsync("gRPC에서 보내는 테스트 메시지!");
        
        // 방 나가기 테스트
        bool leaveResult = await grpcChatManager.LeaveRoomAsync();
        Console.WriteLine($"방 나가기 결과: {leaveResult}");
        Console.WriteLine($"퇴장 후 현재 방: {grpcChatManager.CurrentRoom}");
    }

    public static async Task InteractiveChatTest(NetworkManagerFactory.NetworkType networkType)
    {
        Console.WriteLine($"=== {networkType} 인터랙티브 채팅 테스트 ===");
        
        var chatManager = NetworkManagerFactory.CreateChatManager(networkType);
        
        // 이벤트 구독 (실시간 메시지 표시)
        chatManager.OnChatReceived += (message) => 
            Console.WriteLine($"💬 받은 메시지: {message}");
        chatManager.OnUserJoined += (user) => 
            Console.WriteLine($"✅ {user}님이 입장했습니다");
        chatManager.OnUserLeft += (user) => 
            Console.WriteLine($"❌ {user}님이 퇴장했습니다");
        
        Console.Write("사용자 이름을 입력하세요: ");
        string? userName = Console.ReadLine();
        if (string.IsNullOrEmpty(userName)) userName = "TestUser";
        
        Console.Write("입장할 방 이름을 입력하세요: ");
        string? roomName = Console.ReadLine();
        if (string.IsNullOrEmpty(roomName)) roomName = "TestRoom";
        
        // 방 입장
        bool joined = await chatManager.JoinRoomAsync(roomName, userName);
        if (joined)
        {
            Console.WriteLine($"🎉 {roomName} 방에 입장했습니다!");
            Console.WriteLine("메시지를 입력하세요 (종료하려면 'quit' 입력):");
            
            while (true)
            {
                Console.Write($"[{userName}] ");
                string? input = Console.ReadLine();
                
                if (string.IsNullOrEmpty(input) || input.ToLower() == "quit")
                    break;
                
                await chatManager.SendMessageAsync(input);
            }
            
            await chatManager.LeaveRoomAsync();
            Console.WriteLine("채팅을 종료했습니다.");
        }
        else
        {
            Console.WriteLine("❌ 방 입장에 실패했습니다.");
        }
    }
}