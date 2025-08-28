using System.Net;
using System.Text;
using ServerCore;
using System.Buffers.Binary;
using GamePackets;
using Google.Protobuf;

namespace DummyClient;

public class ServerSession : Session
{
    static unsafe void ToByte(byte[] array, int offset, ulong value)
    {
    }


    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnConnected : {endPoint}");

        /*PlayerInfoReq packet = new PlayerInfoReq()
            { playerId = 1001, name = "ABCD" };
        packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 101, level = 1, duration = 1.0f });
        packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 201, level = 2, duration = 2.0f });
        packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 301, level = 3, duration = 3.0f });
        packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 401, level = 4, duration = 4.0f });
        ArraySegment<byte> s = packet.Write();
                if (s != null) Send(s);
                */
        var packet = new PlayerInfoReq();
        packet.PlayerId = 1001;
        packet.Name = "ABCD";
            
        // 스킬 추가 (사용자 예제와 거의 동일!)
        packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 101, Level = 1, Duration = 1.0f });
        packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 201, Level = 2, Duration = 2.0f });
        packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 301, Level = 3, Duration = 3.0f });
        packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 401, Level = 4, Duration = 4.0f });
        Send( packet.ToByteArray());
    }

    public override void OnDisconnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnDisconnected : {endPoint}");
    }

    public override int OnRecv(ArraySegment<byte> buffer)
    {
        string redvData = Encoding.UTF8.GetString(
            buffer.Array,
            buffer.Offset,
            buffer.Count);
        Console.WriteLine($"[From Server]  {redvData} ");
        return buffer.Count;
    }

    public override void OnSend(int numOfBytes)
    {
        Console.WriteLine($"Transferred bytes : {numOfBytes}");
    }

    public override void OnRecvJsonPacket(string jsonData)
    {
        Console.WriteLine($"[From Server JSON] {jsonData}");
    }
}