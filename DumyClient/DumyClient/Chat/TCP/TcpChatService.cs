using DummyClient.Chat.Common;
using DummyClient.Chat.Interfaces;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GamePackets;
using Google.Protobuf;
using ServerCore;

namespace DummyClient.Chat.TCP;

/// <summary>
/// TCP 채팅 서비스 구현
/// 실제 TCP 소켓을 통한 채팅 기능
/// </summary>
public class TcpChatService : IChatService
{
    public bool IsConnected { get; private set; }
    public string CurrentRoom { get; private set; } = "";
    public string UserName { get; private set; } = "";
    
    public event Action<ChatEventArgs>? OnMessageReceived;
    public event Action<ChatEventArgs>? OnUserJoined;  
    public event Action<ChatEventArgs>? OnUserLeft;
    public event Action<string>? OnDisconnected;

    private string _serverAddress = "";
    private int _port;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;

    public async Task<bool> ConnectAsync(string serverAddress, int port)
    {
        try
        {
            _serverAddress = serverAddress;
            _port = port;
            
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(serverAddress, port);
            _networkStream = _tcpClient.GetStream();
            
            _cancellationTokenSource = new CancellationTokenSource();
            _receiveTask = ReceiveMessagesAsync(_cancellationTokenSource.Token);
            
            IsConnected = true;
            Console.WriteLine($"✅ [TCP] {serverAddress}:{port}에 연결되었습니다");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [TCP] 연결 실패: {ex.Message}");
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
            
            _networkStream?.Close();
            _tcpClient?.Close();
            
            _networkStream = null;
            _tcpClient = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _receiveTask = null;
            
            IsConnected = false;
            Console.WriteLine("🔌 [TCP] 연결이 해제되었습니다");
            OnDisconnected?.Invoke("TCP 연결 해제");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [TCP] 연결 해제 실패: {ex.Message}");
        }
    }

    public async Task<bool> JoinRoomAsync(string roomId, string userName)
    {
        if (!IsConnected)
        {
            Console.WriteLine("❌ [TCP] 서버에 연결되어 있지 않습니다");
            return false;
        }

        try
        {
            var joinPacket = new
            {
                Type = "JoinRoom",
                RoomId = roomId,
                UserName = userName
            };
            
            await SendPacketAsync(joinPacket);
            
            CurrentRoom = roomId;
            UserName = userName;
            
            Console.WriteLine($"🎉 [TCP] {userName}님이 {roomId} 방에 입장했습니다");
            
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
            Console.WriteLine($"❌ [TCP] 방 입장 실패: {ex.Message}");
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
            
            var leavePacket = new
            {
                Type = "LeaveRoom",
                RoomId = roomId,
                UserName = userName
            };
            
            await SendPacketAsync(leavePacket);
            
            Console.WriteLine($"👋 [TCP] {userName}님이 {roomId} 방을 나갔습니다");
            
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
            Console.WriteLine($"❌ [TCP] 방 나가기 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(CurrentRoom))
        {
            Console.WriteLine("❌ [TCP] 방에 입장하지 않았습니다");
            return false;
        }

        try
        {
            // Protocol Buffers C_Chat 패킷 생성
            var chatPacket = new C_Chat
            {
                Message = message
            };
            
            await SendProtobufPacketAsync(PacketID.CChat, chatPacket);
            
            Console.WriteLine($"📤 [TCP] 메시지 전송: {message}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [TCP] 메시지 전송 실패: {ex.Message}");
            return false;
        }
    }
    
    private async Task SendPacketAsync(object packet)
    {
        if (_networkStream == null) return;
        
        var json = JsonSerializer.Serialize(packet);
        var data = Encoding.UTF8.GetBytes(json + "\n");
        
        await _networkStream.WriteAsync(data, 0, data.Length);
        await _networkStream.FlushAsync();
    }
    
    private async Task SendProtobufPacketAsync(PacketID packetId, IMessage message)
    {
        if (_networkStream == null) return;
        
        // 패킷 ID를 2바이트로 직렬화
        byte[] packetIdBytes = BitConverter.GetBytes((ushort)packetId);
        
        // Protocol Buffers 메시지를 바이트 배열로 직렬화
        byte[] messageBytes = message.ToByteArray();
        
        // 패킷 크기 (패킷 ID 2바이트 + 메시지 크기)
        ushort packetSize = (ushort)(sizeof(ushort) + messageBytes.Length);
        byte[] sizeBytes = BitConverter.GetBytes(packetSize);
        
        // 최종 패킷: [크기 2바이트][패킷ID 2바이트][메시지 데이터]
        byte[] packet = new byte[sizeof(ushort) + sizeof(ushort) + messageBytes.Length];
        
        Array.Copy(sizeBytes, 0, packet, 0, sizeof(ushort));
        Array.Copy(packetIdBytes, 0, packet, sizeof(ushort), sizeof(ushort));  
        Array.Copy(messageBytes, 0, packet, sizeof(ushort) + sizeof(ushort), messageBytes.Length);
        
        await _networkStream.WriteAsync(packet, 0, packet.Length);
        await _networkStream.FlushAsync();
        
        Console.WriteLine($"[TCP] Protocol Buffers 패킷 전송 - ID: {packetId}, 크기: {packetSize}바이트");
    }
    
    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && _networkStream != null)
            {
                var bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;
                
                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(data);
                
                string bufferContent = messageBuffer.ToString();
                int newlineIndex;
                
                while ((newlineIndex = bufferContent.IndexOf('\n')) >= 0)
                {
                    var messageJson = bufferContent.Substring(0, newlineIndex);
                    bufferContent = bufferContent.Substring(newlineIndex + 1);
                    
                    if (!string.IsNullOrWhiteSpace(messageJson))
                    {
                        await ProcessReceivedMessage(messageJson);
                    }
                }
                
                messageBuffer.Clear();
                messageBuffer.Append(bufferContent);
            }
        }
        catch (OperationCanceledException)
        {
            // 정상적인 취소
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [TCP] 메시지 수신 오류: {ex.Message}");
            OnDisconnected?.Invoke($"메시지 수신 오류: {ex.Message}");
        }
    }
    
    private async Task ProcessReceivedMessage(string messageJson)
    {
        try
        {
            using var document = JsonDocument.Parse(messageJson);
            var root = document.RootElement;
            
            var type = root.GetProperty("Type").GetString();
            
            switch (type)
            {
                case "ChatMessage":
                    var chatArgs = new ChatEventArgs
                    {
                        RoomId = root.GetProperty("RoomId").GetString() ?? "",
                        UserName = root.GetProperty("UserName").GetString() ?? "",
                        UserId = root.GetProperty("UserName").GetString() ?? "",
                        Message = root.GetProperty("Message").GetString() ?? "",
                        EventType = ChatEventType.Message
                    };
                    OnMessageReceived?.Invoke(chatArgs);
                    break;
                    
                case "UserJoined":
                    var joinArgs = new ChatEventArgs
                    {
                        RoomId = root.GetProperty("RoomId").GetString() ?? "",
                        UserName = root.GetProperty("UserName").GetString() ?? "",
                        UserId = root.GetProperty("UserName").GetString() ?? "",
                        Message = $"{root.GetProperty("UserName").GetString()}님이 입장했습니다",
                        EventType = ChatEventType.UserJoined
                    };
                    OnUserJoined?.Invoke(joinArgs);
                    break;
                    
                case "UserLeft":
                    var leaveArgs = new ChatEventArgs
                    {
                        RoomId = root.GetProperty("RoomId").GetString() ?? "",
                        UserName = root.GetProperty("UserName").GetString() ?? "",
                        UserId = root.GetProperty("UserName").GetString() ?? "",
                        Message = $"{root.GetProperty("UserName").GetString()}님이 퇴장했습니다",
                        EventType = ChatEventType.UserLeft
                    };
                    OnUserLeft?.Invoke(leaveArgs);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [TCP] 메시지 처리 오류: {ex.Message}");
        }
    }
}