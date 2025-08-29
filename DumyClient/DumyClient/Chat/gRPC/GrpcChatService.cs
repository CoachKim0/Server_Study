using DummyClient.Chat.Common;
using DummyClient.Chat.Interfaces;
using Grpc.Net.Client;
using Grpc.Core;
using DummyClient.gRPC;

namespace DummyClient.Chat.gRPC;

/// <summary>
/// gRPC 채팅 서비스 구현
/// gRPC 양방향 스트리밍을 통한 실시간 채팅 기능
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
    private GameService.GameServiceClient? _client;
    private AsyncDuplexStreamingCall<GameMessage, GameMessage>? _streamCall;
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
            _client = new GameService.GameServiceClient(_channel);
            
            _cancellationTokenSource = new CancellationTokenSource();
            _streamCall = _client.Game(cancellationToken: _cancellationTokenSource.Token);
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
        if (!IsConnected) return;

        try
        {
            if (!string.IsNullOrEmpty(CurrentRoom))
            {
                await LeaveRoomAsync();
            }

            _cancellationTokenSource?.Cancel();
            if (_receiveTask != null)
            {
                try { await _receiveTask; } catch { }
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
            // 1. 먼저 인증 수행
            var authMessage = new GameMessage
            {
                UserId = userName,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AuthUser = new AuthUser
                {
                    PlatformType = 1, // PC 플랫폼
                    AuthKey = "testuser", // 서버에 등록된 사용자명
                    RetPassKey = "password123" // 서버에 등록된 패스워드
                }
            };
            
            await SendGrpcMessageAsync(authMessage);
            Console.WriteLine($"🔐 [gRPC] {userName} 인증 요청 전송");
            
            // 인증 응답 대기 (간단히 짧은 대기)
            await Task.Delay(100);
            
            // 2. 방 입장 수행
            var joinMessage = new GameMessage
            {
                UserId = userName,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RoomInfo = new RoomInfo
                {
                    RoomId = roomId,
                    RoomName = roomId,
                    Action = RoomAction.JoinRoom
                }
            };
            
            await SendGrpcMessageAsync(joinMessage);
            
            CurrentRoom = roomId;
            UserName = userName;
            
            Console.WriteLine($"🎉 [gRPC] {userName}님이 {roomId} 방에 입장했습니다");
            
            // 입장 이벤트 발생
            OnUserJoined?.Invoke(new ChatEventArgs
            {
                RoomId = roomId,
                UserId = userName,
                UserName = userName,
                Message = $"{userName}님이 입장했습니다",
                EventType = ChatEventType.UserJoined
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
            
            var leaveMessage = new GameMessage
            {
                UserId = userName,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RoomInfo = new RoomInfo
                {
                    RoomId = roomId,
                    Action = RoomAction.LeaveRoom
                }
            };
            
            await SendGrpcMessageAsync(leaveMessage);
            
            Console.WriteLine($"👋 [gRPC] {userName}님이 {roomId} 방을 나갔습니다");
            
            // 퇴장 이벤트 발생
            OnUserLeft?.Invoke(new ChatEventArgs
            {
                RoomId = roomId,
                UserId = userName,
                UserName = userName,
                Message = $"{userName}님이 퇴장했습니다",
                EventType = ChatEventType.UserLeft
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
            var chatMessage = new GameMessage
            {
                UserId = UserName,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ChatMessage = new ChatMessage
                {
                    RoomId = CurrentRoom,
                    UserId = UserName,
                    Nickname = UserName,
                    Content = message,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Type = ChatType.NormalChat
                }
            };
            
            await SendGrpcMessageAsync(chatMessage);
            
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
    
    private async Task SendGrpcMessageAsync(GameMessage message)
    {
        if (_streamCall?.RequestStream == null) return;
        
        Console.WriteLine($"🔍 [클라이언트] 메시지 전송: UserId={message.UserId}, Type={message.MessageTypeCase}");
        await _streamCall.RequestStream.WriteAsync(message);
    }
    
    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_streamCall?.ResponseStream == null) return;
            
            await foreach (var message in _streamCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                await ProcessReceivedGrpcMessage(message);
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
    
    private async Task ProcessReceivedGrpcMessage(GameMessage message)
    {
        try
        {
            // 배치 메시지 처리
            if (message.BatchMessages != null)
            {
                Console.WriteLine($"📦 [gRPC] 배치 메시지 수신: {message.BatchMessages.Messages.Count}개");
                foreach (var batchedMessage in message.BatchMessages.Messages)
                {
                    await ProcessSingleMessage(batchedMessage);
                }
                return;
            }
            
            await ProcessSingleMessage(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 메시지 처리 오류: {ex.Message}");
        }
    }
    
    private async Task ProcessSingleMessage(GameMessage message)
    {
        try
        {
            switch (message.MessageTypeCase)
            {
                case GameMessage.MessageTypeOneofCase.ChatMessage:
                    OnMessageReceived?.Invoke(new ChatEventArgs
                    {
                        RoomId = message.ChatMessage.RoomId,
                        UserName = message.ChatMessage.Nickname,
                        UserId = message.ChatMessage.UserId,
                        Message = message.ChatMessage.Content,
                        EventType = ChatEventType.Message,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.ChatMessage.Timestamp).DateTime
                    });
                    break;
                    
                case GameMessage.MessageTypeOneofCase.RoomInfo:
                    switch (message.RoomInfo.Action)
                    {
                        case RoomAction.JoinRoom:
                            Console.WriteLine($"🎉 [gRPC] 방 입장 응답: {message.ResultMessage}");
                            if (message.RoomInfo.Users.Count > 0)
                            {
                                foreach (var user in message.RoomInfo.Users)
                                {
                                    if (user != UserName)
                                    {
                                        OnUserJoined?.Invoke(new ChatEventArgs
                                        {
                                            RoomId = message.RoomInfo.RoomId,
                                            UserName = user,
                                            UserId = user,
                                            Message = $"{user}님이 입장했습니다",
                                            EventType = ChatEventType.UserJoined,
                                            Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                                        });
                                    }
                                }
                            }
                            break;
                            
                        case RoomAction.LeaveRoom:
                            Console.WriteLine($"👋 [gRPC] 방 나가기 응답: {message.ResultMessage}");
                            OnUserLeft?.Invoke(new ChatEventArgs
                            {
                                RoomId = message.RoomInfo.RoomId,
                                UserName = message.UserId,
                                UserId = message.UserId,
                                Message = $"{message.UserId}님이 퇴장했습니다",
                                EventType = ChatEventType.UserLeft,
                                Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                            });
                            break;
                    }
                    break;
                    
                // 기존 메시지 타입도 유지 (호환성)
                case GameMessage.MessageTypeOneofCase.RoomMessage:
                    OnMessageReceived?.Invoke(new ChatEventArgs
                    {
                        RoomId = message.RoomMessage.RoomId,
                        UserName = message.UserId,
                        UserId = message.UserId,
                        Message = message.RoomMessage.Content,
                        EventType = ChatEventType.Message,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                    });
                    break;
                    
                case GameMessage.MessageTypeOneofCase.UserJoined:
                    OnUserJoined?.Invoke(new ChatEventArgs
                    {
                        RoomId = message.UserJoined.RoomId,
                        UserName = message.UserJoined.UserId,
                        UserId = message.UserJoined.UserId,
                        Message = $"{message.UserJoined.UserId}님이 입장했습니다",
                        EventType = ChatEventType.UserJoined,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                    });
                    break;
                    
                case GameMessage.MessageTypeOneofCase.UserLeft:
                    OnUserLeft?.Invoke(new ChatEventArgs
                    {
                        RoomId = message.UserLeft.RoomId,
                        UserName = message.UserLeft.UserId,
                        UserId = message.UserLeft.UserId,
                        Message = $"{message.UserLeft.UserId}님이 퇴장했습니다",
                        EventType = ChatEventType.UserLeft,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).DateTime
                    });
                    break;
                    
                case GameMessage.MessageTypeOneofCase.JoinRoom:
                    Console.WriteLine($"🎉 [gRPC] 방 입장 응답: {message.ResultMessage}");
                    break;
                    
                case GameMessage.MessageTypeOneofCase.LeaveRoom:
                    Console.WriteLine($"👋 [gRPC] 방 나가기 응답: {message.ResultMessage}");
                    break;
                    
                case GameMessage.MessageTypeOneofCase.AuthUser:
                    Console.WriteLine($"🔐 [gRPC] 인증 응답: {message.ResultMessage}");
                    break;
                    
                default:
                    Console.WriteLine($"🔔 [gRPC] 시스템 메시지: {message.ResultMessage}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [gRPC] 메시지 처리 오류: {ex.Message}");
        }
    }
}