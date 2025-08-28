using GamePackets;
using Google.Protobuf;

namespace Server_Study;

/// <summary>
/// 게임방 예제 테스트
/// </summary>
public class GameRoom
{
    List<ClientSession> Sessions = new List<ClientSession>();
    object locked = new object();
    
    public int GetSessionCount()
    {
        lock (locked)
        {
            return Sessions.Count;
        }
    }

    public void Broadcast(ClientSession session, string chat)
    {
        S_Chat packet = new S_Chat();
        packet.Playerid = session.SessionId;
        packet.Mesage = chat;
        ArraySegment<byte> segment = packet.ToByteArray();

        lock (locked)
        {
            foreach (var client in Sessions)
            {
                client.Send(segment);
            }
        }
    }


    public void Enter(ClientSession session)
    {
        lock (locked)
        {
            Sessions.Add(session);
            session.Room = this;
            
            // 방 입장 시 현재 방의 모든 사용자에게 업데이트된 참여자 수 브로드캐스트
            BroadcastRoomUpdate();
        }
    }
    
    public void BroadcastRoomUpdate()
    {
        // 현재 방 참여자 수를 모든 클라이언트에게 알림
        Console.WriteLine($"[GameRoom] 현재 방 참여자 수: {Sessions.Count}명");
        
        // 참여자 목록 업데이트 패킷을 만들어서 브로드캐스트할 수 있음
        // (필요하다면 S_RoomUpdate 같은 패킷 타입을 추가해야 함)
    }

    public void Leave(ClientSession session)
    {
        lock (locked)
        {
            Sessions.Remove(session);
            
            // 방 퇴장 시 현재 방의 모든 사용자에게 업데이트된 참여자 수 브로드캐스트
            BroadcastRoomUpdate();
        }
    }
}