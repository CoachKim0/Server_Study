using DummyClient.Chat.Common;
using DummyClient.Chat.Interfaces;
using Grpc.Net.Client;
using Grpc.Core;
using DummyClient.gRPC;

namespace DummyClient.Chat.gRPC;

/// <summary>
/// gRPC 채팅 서비스 구현
/// 새로운 ChatService를 통한 실시간 채팅 기능
/// </summary>
public class GrpcChatService : IChatService
{
    public bool IsConnected { get; private set; }
    public string CurrentRoom { get; private set; } = "";
    public string UserName { get; private set; } = "";
    
    public event Action<ChatEventArgs>? OnMessageReceived;
    public event Action<ChatEventArgs>? OnUserJoined;
    public event Action<ChatEventArgs>? OnUserLeft;
    public event Action<string>? OnDisconnected;

    private GrpcChannel? _channel;
    private string _serverAddress = "";
    private int _port;
    private ChatService.ChatServiceClient? _client;
    private AsyncDuplexStreamingCall<ChatMessage, ChatMessage>? _streamCall;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;

    public async Task<bool> ConnectAsync(string serverAddress, int port)
    {
        try
        {
            _serverAddress = serverAddress;
            _port = port;
            
            var address = $"http://{serverAddress}:{port}";
            _channel = GrpcChannel.ForAddress(address);
            _client = new ChatService.ChatServiceClient(_channel);
            
            _cancellationTokenSource = new CancellationTokenSource();
            _streamCall = _client.StreamChat(cancellationToken: _cancellationTokenSource.Token);
            _receiveTask = ReceiveMessagesAsync(_cancellationTokenSource.Token);
            
            IsConnected = true;
            Console.WriteLine($"✅ [gRPC] {serverAddress}:{port}에 연결되었습니다");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_receiveTask != null)
            {
                _cancellationTokenSource?.Cancel();
                try
                {
                    await _receiveTask;
                }
                catch (OperationCanceledException)
                {
                    // 정상적인 취소
                }
            }
            
            if (_streamCall != null)
            {
                await _streamCall.RequestStream.CompleteAsync();
                _streamCall.Dispose();
                _streamCall = null;
            }
            
            _channel?.Dispose();
            _channel = null;
            _client = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _receiveTask = null;
            
            IsConnected = false;
            Console.WriteLine("🔌 [gRPC] 연결이 해제되었습니다");
            OnDisconnected?.Invoke("gRPC 연결 해제");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 연결 해제 실패: {ex.Message}");
        }
    }

    public async Task<bool> JoinRoomAsync(string roomId, string userName)
    {
        if (!IsConnected)
        {
            Console.WriteLine("❌ [gRPC] 서버에 연결되어 있지 않습니다");
            return false;
        }

        try
        {
            // ChatService를 사용한 방 입장
            var joinMessage = new ChatMessage
            {
                UserId = userName,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TestChat = new TestChatMessage
                {
                    RoomId = roomId,
                    Content = $"{userName}님이 입장했습니다",
                    Type = MessageType.Join
                }
            };
            
            await SendChatMessageAsync(joinMessage);
            
            CurrentRoom = roomId;
            UserName = userName;
            
            Console.WriteLine($"🎉 [gRPC] {userName}님이 {roomId} 방에 입장했습니다");
            
            // 입장 이벤트 발생
            OnUserJoined?.Invoke(new ChatEventArgs
            {
                RoomId = roomId,
                UserName = userName,
                UserId = userName,
                Message = $"{userName}님이 입장했습니다",
                EventType = ChatEventType.UserJoined,
                Timestamp = DateTime.Now
            });
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 방 입장 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LeaveRoomAsync()
    {
        if (string.IsNullOrEmpty(CurrentRoom)) return true;

        try
        {
            string roomId = CurrentRoom;
            string userName = UserName;
            
            var leaveMessage = new ChatMessage
            {
                UserId = userName,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TestChat = new TestChatMessage
                {
                    RoomId = roomId,
                    Content = $"{userName}님이 퇴장했습니다",
                    Type = MessageType.Leave
                }
            };
            
            await SendChatMessageAsync(leaveMessage);
            
            Console.WriteLine($"👋 [gRPC] {userName}님이 {roomId} 방을 나갔습니다");
            
            // 퇴장 이벤트 발생
            OnUserLeft?.Invoke(new ChatEventArgs
            {
                RoomId = roomId,
                UserName = userName,
                UserId = userName,
                Message = $"{userName}님이 퇴장했습니다",
                EventType = ChatEventType.UserLeft,
                Timestamp = DateTime.Now
            });
            
            CurrentRoom = "";
            UserName = "";
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 방 나가기 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(CurrentRoom))
        {
            Console.WriteLine("❌ [gRPC] 방에 입장하지 않았습니다");
            return false;
        }

        try
        {
            var chatMessage = new ChatMessage
            {
                UserId = UserName,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TestChat = new TestChatMessage
                {
                    RoomId = CurrentRoom,
                    Content = message,
                    Type = MessageType.Chat
                }
            };
            
            await SendChatMessageAsync(chatMessage);
            
            Console.WriteLine($"📤 [gRPC] 메시지 전송: {message}");
            Console.WriteLine($"📤 [gRPC] 메시지 전송 완료: {message}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 메시지 전송 실패: {ex.Message}");
            return false;
        }
    }
    
    private async Task SendChatMessageAsync(ChatMessage message)
    {
        if (_streamCall?.RequestStream == null) return;
        
        Console.WriteLine($"🔍 [클라이언트] 메시지 전송: UserId={message.UserId}, Type={message.ChatContextCase}");
        await _streamCall.RequestStream.WriteAsync(message);
    }
    
    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_streamCall?.ResponseStream == null) return;
            
            await foreach (var message in _streamCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await ProcessReceivedChatMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
            // 정상적인 취소
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            // 정상적인 취소
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 메시지 수신 오류: {ex.Message}");
            OnDisconnected?.Invoke($"메시지 수신 오류: {ex.Message}");
        }
    }
    
    private async Task ProcessReceivedChatMessage(ChatMessage message)
    {
        try
        {
            await ProcessSingleChatMessage(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 메시지 처리 오류: {ex.Message}");
        }
    }
    
    private async Task ProcessSingleChatMessage(ChatMessage message)
    {
        try
        {
            switch (message.ChatContextCase)
            {
                case ChatMessage.ChatContextOneofCase.TestChat:
                    var testChat = message.TestChat;
                    switch (testChat.Type)
                    {
                        case MessageType.Chat:
                            OnMessageReceived?.Invoke(new ChatEventArgs
                            {
                                RoomId = testChat.RoomId,
                                UserName = message.UserId,
                                UserId = message.UserId,
                                Message = testChat.Content,
                                EventType = ChatEventType.Message,
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                            });
                            break;
                            
                        case MessageType.Join:
                            OnUserJoined?.Invoke(new ChatEventArgs
                            {
                                RoomId = testChat.RoomId,
                                UserName = message.UserId,
                                UserId = message.UserId,
                                Message = testChat.Content,
                                EventType = ChatEventType.UserJoined,
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                            });
                            Console.WriteLine($"[{UserName}] 입장 알림: {testChat.Content}");
                            break;
                            
                        case MessageType.Leave:
                            OnUserLeft?.Invoke(new ChatEventArgs
                            {
                                RoomId = testChat.RoomId,
                                UserName = message.UserId,
                                UserId = message.UserId,
                                Message = testChat.Content,
                                EventType = ChatEventType.UserLeft,
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                            });
                            Console.WriteLine($"[{UserName}] 퇴장 알림: {testChat.Content}");
                            break;
                            
                        case MessageType.System:
                            Console.WriteLine($"🔧 [gRPC] 시스템 메시지: {testChat.Content}");
                            break;
                    }
                    break;
                    
                default:
                    Console.WriteLine($"🔧 [gRPC] 알 수 없는 메시지 타입: {message.ChatContextCase}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 메시지 처리 오류: {ex.Message}");
        }
    }
}