using System.Net;
using System.Text;
using ServerCore;
using System.Buffers.Binary;

namespace DummyClient;

public class ServerSession : Session
{
    static unsafe void ToByte(byte[] array, int offset, ulong value)
    {
    }


    public override void OnConnected(EndPoint endPoint)
    {
        Console.WriteLine($"OnConnected : {endPoint}");

        PlayerInfoReq packet = new PlayerInfoReq()
            { playerId = 1001 };
        ArraySegment<byte> s = packet.Write();

        if (s != null) Send(s);
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
}