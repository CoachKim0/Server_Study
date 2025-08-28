using System.Buffers.Binary;
using System.Net;
using GamePackets;
using ServerCore;
using Server_Study.Modules.GamePlay.Room;

namespace Server_Study;

/// <summary>
/// 게임 클라이언트와의 세션을 처리하는 구체적인 세션 클래스
/// Session 추상 클래스를 상속받아 게임 로직에 맞는 네트워크 처리를 구현
/// </summary>
public class ClientSession : CustomPacketSession
{
    public int SessionId;
    public GameRoom Room { get; set; }
    private JobQueue _packetJobQueue = new JobQueue();


    /// <summary>
    /// 클라이언트가 서버에 연결되었을 때 호출되는 메서드
    /// 연결된 클라이언트에게 초기 게임 데이터(기사 정보)를 전송
    /// </summary>
    /// <param name="endPoint">연결된 클라이언트의 엔드포인트 정보</param>
    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnConnected : {endPoint}");
        
        // 자동으로 기본 게임방에 입장
        Program.room.Enter(this);
        
        // UserManager에도 등록 (TCP 세션용 임시 사용자 ID 생성)
        string tempUserId = $"tcp_user_{SessionId}";
        Server_Study.Shared.Utils.UserManager.Instance.AuthenticateUser(tempUserId, $"Player{SessionId}");
        Server_Study.Shared.Utils.UserManager.Instance.JoinRoom(tempUserId, "default_room");
        
        // UserManager와 TCP 세션 연결
        var userInfo = Server_Study.Shared.Utils.UserManager.Instance.GetUserInfo(tempUserId);
        if (userInfo != null)
        {
            userInfo.TcpSession = this;
        }
    }

    public override void OnRecvPacket(ushort packetId, ArraySegment<byte> protobufData)
    {
        Console.WriteLine($"[DEBUG] 패킷 수신: PacketID={packetId}, 데이터 크기={protobufData.Count}");

        // 패킷 데이터를 복사하여 JobQueue로 전달 (원본 버퍼가 재사용될 수 있으므로)
        byte[] packetData = new byte[protobufData.Count];
        Array.Copy(protobufData.Array, protobufData.Offset, packetData, 0, protobufData.Count);
        
        // JobQueue를 사용하여 패킷 처리를 별도 스레드에서 수행
        _packetJobQueue.Push(() => {
            ProcessPacket(packetId, new ArraySegment<byte>(packetData));
        });
    }

    private void ProcessPacket(ushort packetId, ArraySegment<byte> protobufData)
    {
        Console.WriteLine($"[DEBUG] 패킷 처리 시작: PacketID={packetId}, SessionID={SessionId}");

        try
        {
            switch ((PacketID)packetId)
            {
                case PacketID.PlayerInfoReq:
                    HandlePlayerInfoReq(protobufData);
                    break;
                    
                case PacketID.CChat:
                    HandleCChat(protobufData);
                    break;
                    
                case PacketID.SChat:
                    HandleSChat(protobufData);
                    break;
                    
                default:
                    Console.WriteLine($"[WARNING] 알 수 없는 패킷 ID: {packetId}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] 패킷 처리 중 오류: {ex.Message}");
        }
        
        Console.WriteLine($"[DEBUG] 패킷 처리 완료: PacketID={packetId}");
    }
    
    private void HandlePlayerInfoReq(ArraySegment<byte> data)
    {
        try
        {
            PlayerInfoReq p = PlayerInfoReq.Parser.ParseFrom(data.Array, data.Offset, data.Count);
            Console.WriteLine($"[SUCCESS] PlayerInfoReq 파싱 성공!");
            Console.WriteLine($"PlayerId: {p.PlayerId}");
            Console.WriteLine($"Name: {p.Name}");

            // skill 정보 출력
            Console.WriteLine($"Skills 개수: {p.Skills.Count}");
            foreach (var skill in p.Skills)
            {
                Console.WriteLine($"  Skill: ID={skill.Id}, Level={skill.Level}, Duration={skill.Duration}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] PlayerInfoReq 파싱 실패: {ex.Message}");
        }
    }
    
    private void HandleCChat(ArraySegment<byte> data)
    {
        try
        {
            C_Chat cChat = C_Chat.Parser.ParseFrom(data.Array, data.Offset, data.Count);
            Console.WriteLine($"[SUCCESS] C_Chat 수신!");
            Console.WriteLine($"SessionID: {SessionId}");
            Console.WriteLine($"Message: {cChat.Message}");
            
            // 채팅을 방의 다른 플레이어들에게 브로드캐스트 (JobQueue 사용)
            Room?.Broadcast(this, cChat.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] C_Chat 파싱 실패: {ex.Message}");
        }
    }
    
    private void HandleSChat(ArraySegment<byte> data)
    {
        try
        {
            S_Chat sChat = S_Chat.Parser.ParseFrom(data.Array, data.Offset, data.Count);
            Console.WriteLine($"[SUCCESS] S_Chat 수신!");
            Console.WriteLine($"PlayerId: {sChat.Playerid}");
            Console.WriteLine($"Message: {sChat.Mesage}");
            
            // 채팅을 방의 다른 플레이어들에게 브로드캐스트 (JobQueue 사용)
            Room?.Broadcast(this, sChat.Mesage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] S_Chat 파싱 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 클라이언트와의 연결이 종료되었을 때 호출되는 메서드
    /// 연결 종료 후 정리 작업을 수행
    /// </summary>
    /// <param name="endPoint">연결이 종료된 클라이언트의 엔드포인트 정보</param>
    public override void OnDisconnected(EndPoint endPoint)
    {
        Session_Manager.Instance.Remove(this);
        if (Room != null)
        {
            Room.Leave(this);
            Room = null;
        }

        // UserManager에서도 제거
        string tempUserId = $"tcp_user_{SessionId}";
        Server_Study.Shared.Utils.UserManager.Instance.LeaveRoom(tempUserId, "default_room");
        Server_Study.Shared.Utils.UserManager.Instance.LogoutUser(tempUserId);

        Console.WriteLine($"OnDisconnected : {endPoint}");
    }

    /// <summary>
    /// 클라이언트로 데이터 전송이 완료되었을 때 호출되는 메서드
    /// 전송된 바이트 수를 로그로 출력
    /// </summary>
    /// <param name="numOfBytes">전송된 바이트 수</param>
    public override void OnSend(int numOfBytes)
    {
        Console.WriteLine($"Transferred bytes : {numOfBytes}");
    }

    public override void OnRecvJsonPacket(string jsonData)
    {
        Console.WriteLine($"[DEBUG] JSON 패킷 수신: {jsonData}");
        
        // JobQueue를 사용하여 JSON 패킷 처리를 별도 스레드에서 수행
        _packetJobQueue.Push(() => {
            ProcessJsonPacket(jsonData);
        });
    }

    private void ProcessJsonPacket(string jsonData)
    {
        try
        {
            // 간단한 JSON 파싱 (System.Text.Json 사용)
            using (var document = System.Text.Json.JsonDocument.Parse(jsonData))
            {
                var root = document.RootElement;
                
                if (root.TryGetProperty("Type", out var typeElement))
                {
                    string messageType = typeElement.GetString() ?? "";
                    Console.WriteLine($"[DEBUG] JSON 메시지 타입: {messageType}");
                    
                    switch (messageType)
                    {
                        case "JoinRoom":
                            if (root.TryGetProperty("RoomId", out var roomIdElement))
                            {
                                string roomId = roomIdElement.GetString() ?? "";
                                Console.WriteLine($"[SUCCESS] JoinRoom 요청 - RoomId: {roomId}");
                                
                                // 응답 JSON 생성 및 전송
                                string response = $@"{{""Type"":""JoinRoomResponse"",""Success"":true,""RoomId"":""{roomId}""}}";
                                SendJsonResponse(response);
                            }
                            break;
                            
                        case "LeaveRoom":
                            if (root.TryGetProperty("RoomId", out var leaveRoomIdElement))
                            {
                                string roomId = leaveRoomIdElement.GetString() ?? "";
                                Console.WriteLine($"[SUCCESS] LeaveRoom 요청 - RoomId: {roomId}");
                                
                                // 응답 JSON 생성 및 전송
                                string response = $@"{{""Type"":""LeaveRoomResponse"",""Success"":true,""RoomId"":""{roomId}""}}";
                                SendJsonResponse(response);
                            }
                            break;
                            
                        case "SendMessage":
                            if (root.TryGetProperty("Message", out var messageElement))
                            {
                                string message = messageElement.GetString() ?? "";
                                Console.WriteLine($"[SUCCESS] SendMessage 요청 - Message: {message}");
                                
                                // 방의 다른 플레이어들에게 메시지 브로드캐스트
                                Room?.Broadcast(this, message);
                            }
                            break;
                            
                        default:
                            Console.WriteLine($"[WARNING] 알 수 없는 JSON 메시지 타입: {messageType}");
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] JSON 패킷 처리 중 오류: {ex.Message}");
        }
    }

    private void SendJsonResponse(string jsonResponse)
    {
        try
        {
            byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(jsonResponse);
            ArraySegment<byte> responseSegment = new ArraySegment<byte>(responseBytes);
            Send(responseSegment);
            Console.WriteLine($"[DEBUG] JSON 응답 전송: {jsonResponse}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] JSON 응답 전송 중 오류: {ex.Message}");
        }
    }
}