using System.Buffers.Binary;
using System.Text;
using ServerCore;

public abstract class Packet
{
    public ushort size;
    public ushort packetId;

    public abstract ArraySegment<byte> Write();
    public abstract void Read(ArraySegment<byte> segment);
}

public class PlayerInfoReq : Packet
{
    public long playerId;
    public string name;

    public struct SkillInfo
    {
        public int id;
        public short level;
        public float duration;

        public bool Write(Span<byte> s, ref ushort count)
        {
            bool success = true;
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.id);
            count += sizeof(int);
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.level);
            count += sizeof(short);
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.duration);
            count += sizeof(float);
            return success;
        }

        public void Read(ReadOnlySpan<byte> s, ref ushort count)
        {
            this.id = BitConverter.ToInt32(s.Slice(count, s.Length - count));
            count += sizeof(int);
            this.level = BitConverter.ToInt16(s.Slice(count, s.Length - count));
            count += sizeof(short);
            this.duration = BitConverter.ToSingle(s.Slice(count, s.Length - count));
            count += sizeof(float);
        }
    }

    public List<SkillInfo> skills = new List<SkillInfo>();

    public PlayerInfoReq()
    {
        this.packetId = (ushort)PacketID.PlayerInfoReq;
    }

    public override void Read(ArraySegment<byte> segment)
    {
        ushort count = 0;

        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);

        count += sizeof(ushort);
        count += sizeof(ushort);
        this.playerId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
        count += sizeof(long);

        // string
        ushort nameLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
        count += sizeof(ushort);
        this.name = Encoding.Unicode.GetString(s.Slice(count, nameLen));
        count += nameLen;

        // skill list
        skills.Clear();
        ushort skillLen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));
        count += sizeof(ushort);
        for (int i = 0; i < skillLen; i++)
        {
            SkillInfo skill = new SkillInfo();
            skill.Read(s, ref count);
            skills.Add(skill);
        }
    }

    public override ArraySegment<byte> Write()
    {
        ArraySegment<byte> segment = SendBufferHelper.Open(4096);

        ushort count = 0;
        bool success = true;

        Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

        // 사이즈는 2 가 고정이라서 
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.packetId);
        count += sizeof(ushort);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
        count += sizeof(long);

        // string
        ushort nameLen = (ushort)Encoding.Unicode.GetByteCount(this.name);
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nameLen);
        count += sizeof(ushort);
        Array.Copy(Encoding.Unicode.GetBytes(this.name), 0, segment.Array, count, nameLen);
        count += nameLen;

        // skills
        success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)skills.Count);
        count += sizeof(ushort);
        foreach (SkillInfo skill in skills)
        {
            // TODO
            success &= skill.Write(s, ref count);
        }

        success &= BitConverter.TryWriteBytes(s, count);

        if (success == false)
            return null;

        return SendBufferHelper.Close(count);
    }
    /*public override ArraySegment<byte> Write()
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
    }*/
}

public enum PacketID
{
    PlayerInfoReq = 1,
    PlayerInfoOk = 2,
}