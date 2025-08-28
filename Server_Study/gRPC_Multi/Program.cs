using Grpc.Net.Client;
using GrpcApp;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("gRPC 다중 클라이언트 테스트 시작");
        
        // 명령줄 인수로 클라이언트 모드 선택 (1: 첫 번째 클라이언트, 2: 두 번째 클라이언트)
        var clientMode = args.Length > 0 && int.TryParse(args[0], out var mode) ? mode : 1;
        var userId = $"User{clientMode}_{DateTime.Now:mmss}";
        
        Console.WriteLine($"클라이언트 모드: {clientMode}, 사용자 ID: {userId}");
        
        try
        {
            using var channel = GrpcChannel.ForAddress("http://localhost:5554");
            var client = new GameService.GameServiceClient(channel);
            
            using var call = client.Game();
            
            // 응답 수신 태스크
            var cancellationToken = new CancellationTokenSource();
            var responseTask = Task.Run(async () =>
            {
                try
                {
                    while (await call.ResponseStream.MoveNext(cancellationToken.Token))
                    {
                        await HandleServerMessage(call.ResponseStream.Current);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"응답 수신 오류: {ex.Message}");
                }
            });
            
            // 인증
            await Authenticate(call, userId);
            await Task.Delay(1000);
            
            if (clientMode == 1)
            {
                // 첫 번째 클라이언트: 방 생성 후 입장
                await JoinRoom(call, userId, "TestRoom", "테스트 방", true);
            }
            else
            {
                // 두 번째 클라이언트: 기존 방 입장 (2초 후)
                await Task.Delay(2000);
                await JoinRoom(call, userId, "TestRoom", "", false);
            }
            
            // 메시지 전송 (5초 후)
            await Task.Delay(5000);
            await SendRoomMessage(call, userId, "TestRoom", $"{userId}에서 보낸 테스트 메시지!");
            
            Console.WriteLine("=== 키보드 명령어 ===");
            Console.WriteLine("1번 키: 메시지 전송 ('안녕하세요 난 [ID] 야!!')");
            Console.WriteLine("Enter: 종료");
            Console.WriteLine("==================");
            
            // 키보드 입력 처리
            while (true)
            {
                var key = Console.ReadKey(true);
                
                if (key.Key == ConsoleKey.D1) // 1번 키
                {
                    var customMessage = $"안녕하세요 난 [{userId}] 야!!";
                    await SendRoomMessage(call, userId, "TestRoom", customMessage);
                }
                else if (key.Key == ConsoleKey.Enter) // Enter 키
                {
                    break;
                }
            }
            
            // 방 나가기
            await LeaveRoom(call, userId, "TestRoom");
            
            cancellationToken.Cancel();
            await call.RequestStream.CompleteAsync();
            await responseTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류 발생: {ex.Message}");
        }
    }
    
    static async Task Authenticate(Grpc.Core.AsyncDuplexStreamingCall<GameMessage, GameMessage> call, string userId)
    {
        var authMessage = new GameMessage
        {
            UserId = userId,
            AuthUser = new AuthUser
            {
                PlatformType = 1,
                AuthKey = "test_key"
            }
        };
        
        await call.RequestStream.WriteAsync(authMessage);
        Console.WriteLine($"인증 요청 전송: {userId}");
    }
    
    static async Task JoinRoom(Grpc.Core.AsyncDuplexStreamingCall<GameMessage, GameMessage> call, string userId, string roomId, string roomName, bool createIfNotExists)
    {
        var joinMessage = new GameMessage
        {
            UserId = userId,
            JoinRoom = new JoinRoom
            {
                RoomId = roomId,
                RoomName = roomName,
                CreateIfNotExists = createIfNotExists,
                MaxUsers = 10
            }
        };
        
        await call.RequestStream.WriteAsync(joinMessage);
        Console.WriteLine($"방 입장 요청 전송: {userId} -> {roomId}");
    }
    
    static async Task LeaveRoom(Grpc.Core.AsyncDuplexStreamingCall<GameMessage, GameMessage> call, string userId, string roomId)
    {
        var leaveMessage = new GameMessage
        {
            UserId = userId,
            LeaveRoom = new LeaveRoom
            {
                RoomId = roomId
            }
        };
        
        await call.RequestStream.WriteAsync(leaveMessage);
        Console.WriteLine($"방 나가기 요청 전송: {userId} <- {roomId}");
    }
    
    static async Task SendRoomMessage(Grpc.Core.AsyncDuplexStreamingCall<GameMessage, GameMessage> call, string userId, string roomId, string content)
    {
        var roomMessage = new GameMessage
        {
            UserId = userId,
            RoomMessage = new RoomMessage
            {
                RoomId = roomId,
                Content = content,
                SenderId = userId
            }
        };
        
        await call.RequestStream.WriteAsync(roomMessage);
        Console.WriteLine($"방 메시지 전송: {userId} in {roomId}: {content}");
    }
    
    static async Task HandleServerMessage(GameMessage message)
    {
        await Task.Delay(1); // async 컨텍스트 유지
        
        switch (message.MessageTypeCase)
        {
            case GameMessage.MessageTypeOneofCase.AuthUser:
                Console.WriteLine($"[인증 응답] {message.UserId}: {message.ResultMessage}");
                break;
                
            case GameMessage.MessageTypeOneofCase.JoinRoom:
                Console.WriteLine($"[방 입장 응답] {message.UserId}: {message.ResultMessage}");
                break;
                
            case GameMessage.MessageTypeOneofCase.UserJoined:
                var joined = message.UserJoined;
                Console.WriteLine($"[사용자 입장 알림] {joined.UserId}님이 {joined.RoomId} 방에 입장했습니다.");
                Console.WriteLine($"현재 방 인원: [{string.Join(", ", joined.CurrentUsers)}]");
                break;
                
            case GameMessage.MessageTypeOneofCase.UserLeft:
                var left = message.UserLeft;
                Console.WriteLine($"[사용자 나감 알림] {left.UserId}님이 {left.RoomId} 방을 나갔습니다.");
                Console.WriteLine($"현재 방 인원: [{string.Join(", ", left.CurrentUsers)}]");
                break;
                
            case GameMessage.MessageTypeOneofCase.RoomMessage:
                var roomMsg = message.RoomMessage;
                Console.WriteLine($"[방 메시지] {roomMsg.SenderId}: {roomMsg.Content}");
                break;
                
            default:
                Console.WriteLine($"[서버 응답] {message.UserId}: {message.ResultMessage}");
                break;
        }
    }
}