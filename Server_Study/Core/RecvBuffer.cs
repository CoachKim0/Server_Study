namespace ServerCore;

public class RecvBuffer
{
    ArraySegment<byte> buffer;
    private int readPos;
    private int writePos;

    public RecvBuffer(int bufferSize)
    {
        buffer = new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
    }

    /// <summary>
    /// 데이터가 얼만큼 쌓여 있는가?
    /// </summary>
    public int DataSize
    {
        get { return writePos - readPos; }
    }

    /// <summary>
    /// 버퍼에 남은 공간
    /// </summary>
    public int FreeSize
    {
        get { return buffer.Count - writePos; }
    }

    /// <summary>
    /// 버퍼에서 읽을수 있는 범위
    /// </summary>
    public ArraySegment<byte> ReadSegment
    {
        get { return new ArraySegment<byte>(buffer.Array!, buffer.Offset + readPos, DataSize); }
    }

    /// <summary>
    /// 버퍼에서 받을수 있는 범위
    /// </summary>
    public ArraySegment<byte> WriteSegment
    {
        get { return new ArraySegment<byte>(buffer.Array!, buffer.Offset + writePos, FreeSize); }
    }

    /// <summary>
    /// 버퍼 초기화
    /// </summary>
    public void Clean()
    {
        int dataSize = DataSize;
        if (dataSize == 0)
        {
            // 남은 데이터가 없으면 복사하지 않고 커서 위치만 리셋
            readPos = writePos = 0;
        }
        else
        {
            // 남은 찌꺼기가 있으면 시작 위치로 복사
            Array.Copy(buffer.Array!, buffer.Offset + readPos, buffer.Array!, buffer.Offset, dataSize);
            readPos = 0;
            writePos = dataSize;
        }
    }

    /// <summary>
    /// 성공적으로 컨텐츠 데이터를 처리를 했을때
    /// </summary>
    /// <param name="numOfBytes"></param>
    /// <returns></returns>
    public bool OnRead(int numOfBytes)
    {
        if (numOfBytes > DataSize) return false;
        readPos += numOfBytes;
        return true;
    }

    public bool OnWrite(int numOfBytes)
    {
        if ( numOfBytes > FreeSize) return false;
        writePos += numOfBytes;
        return true;
    }
}