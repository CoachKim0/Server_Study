using System.Buffers.Binary;
using System.Net;
using ServerCore;

namespace Server_Study;

/// <summary>
/// 게임 클라이언트와의 세션을 처리하는 구체적인 세션 클래스
/// Session 추상 클래스를 상속받아 게임 로직에 맞는 네트워크 처리를 구현
/// </summary>
class ClientSession : PacketSession
{
    /// <summary>
    /// 클라이언트가 서버에 연결되었을 때 호출되는 메서드
    /// 연결된 클라이언트에게 초기 게임 데이터(기사 정보)를 전송
    /// </summary>
    /// <param name="endPoint">연결된 클라이언트의 엔드포인트 정보</param>
    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnConnected : {endPoint}");

        // 클라이언트에게 데이터 전송
        //Send(sendBuff);
        Thread.Sleep(5000);

        // 테스트를 위해 연결 종료
        Disconnect();
    }

    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        ushort count = 0;
        ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
        count += 2;
        ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
        count += 2;

        switch ((PacketID)id)
        {
            case PacketID.PlayerInfoReq:
            {
                PlayerInfoReq p = new PlayerInfoReq();
                p.Read(buffer);
                Console.WriteLine($"PlayerInfoReq : {p.name}  {p.playerId}");

                // skill
                foreach (var skill in p.skills)
                {
                    Console.WriteLine($"skill :  {skill.id} , {skill.level} , {skill.duration}");
                }
            }
                break;
        }

        Console.WriteLine($"OnRecvPacket : {size} , {id}");
    }

// 헬퍼 구조체
    ref struct PacketReader
    {
        private ReadOnlySpan<byte> _buffer;
        private int _position;

        public PacketReader(ArraySegment<byte> buffer)
        {
            _buffer = new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, buffer.Count);
            _position = 0;
        }

        public ushort ReadUInt16()
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_position));
            _position += 2;
            return value;
        }

        public long ReadInt64()
        {
            long value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.Slice(_position));
            _position += 8;
            return value;
        }
    }

    /// <summary>
    /// 클라이언트와의 연결이 종료되었을 때 호출되는 메서드
    /// 연결 종료 후 정리 작업을 수행
    /// </summary>
    /// <param name="endPoint">연결이 종료된 클라이언트의 엔드포인트 정보</param>
    public override void OnDisconnected(EndPoint endPoint)
    {
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
}