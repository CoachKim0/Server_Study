using System.Buffers.Binary;
using System.Net;
using GamePackets;
using ServerCore;

namespace Server_Study;

/// <summary>
/// 게임 클라이언트와의 세션을 처리하는 구체적인 세션 클래스
/// Session 추상 클래스를 상속받아 게임 로직에 맞는 네트워크 처리를 구현
/// </summary>
class ClientSession : ProtobufPacketSession
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

    public override void OnRecvProtobuf(ArraySegment<byte> protobufData)
    {
        Console.WriteLine($"[DEBUG] Protobuf 데이터 처리 시작: 데이터 크기={protobufData.Count}");

        try
        {
            // PlayerInfoReq로 파싱 시도
            PlayerInfoReq p = PlayerInfoReq.Parser.ParseFrom(protobufData.Array, protobufData.Offset, protobufData.Count);
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
            Console.WriteLine($"[ERROR] Protobuf 파싱 실패: {ex.Message}");
            Console.WriteLine($"[ERROR] 스택 트레이스: {ex.StackTrace}");
        }
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