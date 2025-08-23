using System.Buffers.Binary;
using ServerCore;

namespace DummyClient;

public abstract class Packet
{
    public ushort size;
    public ushort packetId;

    public abstract ArraySegment<byte> Write();
    public abstract void Read(ArraySegment<byte> bytes);
}

public class PlayerInfoReq : Packet
{
    public long playerId;

    public PlayerInfoReq()
    {
        this.packetId = (ushort)PacketID.PlayerInfoReq;
    }

    public override void Read(ArraySegment<byte> bytes)
    {
        ushort count = 0;
        count += 2;
        count += 2;
        this.playerId = BitConverter.ToInt64(bytes.Array, bytes.Offset+ count) ;
        count += 8;
    }

    public override ArraySegment<byte> Write()
    {
        ArraySegment<byte> s = SendBufferHelper.Open(4096);

        ushort count = 0;
        bool success = true;


        var span = new Span<byte>(s.Array, s.Offset, s.Count);
        success = BinaryPrimitives.TryWriteUInt16LittleEndian(span, size) &&
                  BinaryPrimitives.TryWriteUInt16LittleEndian(span.Slice(2), packetId) &&
                  BinaryPrimitives.TryWriteInt64LittleEndian(span.Slice(4), playerId);
        if (success == false) return null;

        count += 12;
        ArraySegment<byte> sendBuff = SendBufferHelper.Close(count);
        return sendBuff;
    }
}



public enum PacketID
{
    PlayerInfoReq = 1,
    PlayerInfoOk = 2,
}