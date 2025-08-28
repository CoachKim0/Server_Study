using GamePackets;
using Google.Protobuf;
using ServerCore;

namespace Server_Study.Modules.GamePlay.Room;

/// <summary>
/// 게임방 예제 테스트
/// </summary>
public class GameRoom
{
    List<ClientSession> Sessions = new List<ClientSession>();
    object locked = new object();
    private JobQueue _jobQueue = new JobQueue();
    
    public int GetSessionCount()
    {
        lock (locked)
        {
            return Sessions.Count;
        }
    }

    public void Broadcast(ClientSession session, string chat)
    {
        // JobQueue를 사용하여 비동기로 브로드캐스트 처리
        _jobQueue.Push(() => {
            S_Chat packet = new S_Chat();
            packet.Playerid = session.SessionId;
            packet.Mesage = chat;
            ArraySegment<byte> segment = packet.ToByteArray();

            List<ClientSession> sessionsCopy;
            lock (locked)
            {
                sessionsCopy = new List<ClientSession>(Sessions);
            }

            // 실제 전송은 락 밖에서 수행하여 성능 향상
            foreach (var client in sessionsCopy)
            {
                try
                {
                    client.Send(segment);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameRoom] 브로드캐스트 전송 오류: {ex.Message}");
                }
            }
            
            Console.WriteLine($"[GameRoom] 채팅 브로드캐스트 완료: {sessionsCopy.Count}명에게 전송");
        });
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
        // JobQueue를 사용하여 룸 업데이트도 비동기로 처리
        _jobQueue.Push(() => {
            int currentCount;
            lock (locked)
            {
                currentCount = Sessions.Count;
            }
            
            Console.WriteLine($"[GameRoom] 현재 방 참여자 수: {currentCount}명");
            
            // 참여자 목록 업데이트 패킷을 만들어서 브로드캐스트할 수 있음
            // (필요하다면 S_RoomUpdate 같은 패킷 타입을 추가해야 함)
        });
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