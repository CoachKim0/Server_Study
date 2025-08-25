using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerCore;

/// <summary>
/// 연결된 클라이언트와의 통신을 처리하는 추상 세션 클래스
/// TCP 소켓을 이용한 비동기 네트워크 통신을 처리
/// 데이터 송수신, 연결 관리, 버퍼링 기능을 제공
/// </summary>
public abstract class Session
{
    /// <summary>
    /// 클라이언트와의 통신에 사용되는 TCP 소켓
    /// </summary>
    Socket socket;

    /// <summary>
    /// 연결 끊기 상태를 나타내는 플래그 (0: 연결됨, 1: 끊어짐)
    /// Interlocked 연산을 통한 스레드 안전 처리
    /// </summary>
    private int _disconnect = 0;

    /// <summary>
    /// 수신된 데이터를 임시 저장하는 버퍼 (8192바이트 크기로 증가)
    /// </summary>
    private RecvBuffer recvBuffer = new RecvBuffer(8192);

    /// <summary>
    /// 송신 작업을 동기화하기 위한 락 객체
    /// </summary>
    object _lock = new object();

    /// <summary>
    /// 송신 대기 중인 데이터를 저장하는 큐
    /// </summary>
    private Queue<ArraySegment<byte>> sendQueue = new Queue<ArraySegment<byte>>();

    /// <summary>
    /// 현재 송신 중인 데이터 목록
    /// </summary>
    List<ArraySegment<byte>> pendingList = new List<ArraySegment<byte>>();

    /// <summary>
    /// 비동기 송신 작업에 사용되는 이벤트 인수
    /// </summary>
    SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();

    /// <summary>
    /// 비동기 수신 작업에 사용되는 이벤트 인수
    /// </summary>
    SocketAsyncEventArgs recvArgs = new SocketAsyncEventArgs();

    /// <summary>
    /// 클라이언트 연결 완료 시 호출되는 콜백 함수
    /// 하위 클래스에서 연결 후 초기화 로직을 구현
    /// </summary>
    /// <param name="endPoint">연결된 클라이언트의 엔드포인트 정보</param>
    public abstract void OnConnected(EndPoint endPoint);

    /// <summary>
    /// 클라이언트에서 데이터를 수신했을 때 호출되는 콜백 함수
    /// 하위 클래스에서 패킷 처리 로직을 구현
    /// </summary>
    /// <param name="buffer">수신된 데이터 버퍼</param>
    /// <returns>처리된 데이터의 바이트 수 (실패 시 음수)</returns>
    public abstract int OnRecv(ArraySegment<byte> buffer);

    /// <summary>
    /// 데이터 송신 완료 시 호출되는 콜백 함수
    /// 하위 클래스에서 송신 후 처리 로직을 구현
    /// </summary>
    /// <param name="numOfBytes">송신된 바이트 수</param>
    public abstract void OnSend(int numOfBytes);

    /// <summary>
    /// 클라이언트 연결 종료 시 호출되는 콜백 함수
    /// 하위 클래스에서 연결 종료 후 정리 로직을 구현
    /// </summary>
    /// <param name="endPoint">연결이 종료된 클라이언트의 엔드포인트 정보</param>
    public abstract void OnDisconnected(EndPoint endPoint);


    /// <summary>
    /// 세션을 시작하고 수신 대기 상태로 전환
    /// 비동기 이벤트 핸들러를 등록하고 수신를 시작
    /// </summary>
    /// <param name="socket">클라이언트와 연결된 소켓</param>
    public void Start(Socket socket)
    {
        this.socket = socket;
        // 비동기 수신/송신 이벤트 핸들러 등록
        recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);
        sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);

        // 수신 대기 시작
        RegisterRecv();
    }

    #region 보내기 처리

    // 보내기는 상대적으로 처리하기가 복잡하다.
    // 미래를 예측하지 못하자나 ㅋ


    /// <summary>
    /// 큐에 쌓아서 한번에 보내자 그래야 성능적 이득을 본다.
    /// </summary>
    /// <param name="sendBuff"></param>
    public void Send(ArraySegment<byte> sendBuff)
    {
        // 멀티 스레드 환경에선 락을 이용하여 한번에 한명씩
        lock (_lock)
        {
            sendQueue.Enqueue(sendBuff);
            if (pendingList.Count == 0)
                RegisterSend();
        }
    }

    public void Disconnect()
    {
        if (Interlocked.Exchange(ref _disconnect, 1) == 1) ;
        return;

        OnDisconnected(socket.RemoteEndPoint);
        socket.Shutdown(SocketShutdown.Both);
        socket.Close();
    }

    #endregion


    #region 네트워크 통신

    void RegisterSend()
    {
        Console.WriteLine($"{sendQueue.Count}");
        while (sendQueue.Count > 0)
        {
            ArraySegment<byte> buff = sendQueue.Dequeue();
            pendingList.Add(buff);
        }

        sendArgs.BufferList = pendingList;

        bool pending = socket.SendAsync(sendArgs);
        if (pending == false)
            OnSendCompleted(null, sendArgs);
    }

    void OnSendCompleted(object sender, SocketAsyncEventArgs args)
    {
        lock (_lock)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    sendArgs.BufferList = null;
                    pendingList.Clear();

                    Console.WriteLine($"{sendQueue.Count}");
                    OnSend(sendArgs.BytesTransferred);


                    if (sendQueue.Count > 0)
                        RegisterSend();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"OnSendCompleted Failed: {e}");
                }
            }
            else
            {
                Disconnect();
            }
        }
    }


    void RegisterRecv()
    {
        recvBuffer.Clean();
        // 유효한 범위를 짚어 줘야 한다.
        ArraySegment<byte> segment = recvBuffer.WriteSegment;
        recvArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);


        bool pending = socket.ReceiveAsync(recvArgs);
        if (pending == false) // 바로 성공했을 경우
            OnRecvCompleted(null, recvArgs);
    }

    void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
    {
        //args.BytesTransferred 몇 바이트를 받았느냐??
        if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
        {
            // 성공적으로 데이터를 갖고 왔을 경우
            try
            {
                // Write 커서 이동
                if (recvBuffer.OnWrite(args.BytesTransferred) == false)
                {
                    Disconnect();
                    return;
                }

                // 컨텐츠 쪽으로 데이터를 넘겨주고 얼마나 처리했는지 받는다.
                //OnRecv(new ArraySegment<byte>(args.Buffer, args.Offset, args.BytesTransferred));
                int processLen = OnRecv(recvBuffer.ReadSegment);
                if (processLen < 0 || recvBuffer.DataSize < processLen)
                {
                    Disconnect();
                    return;
                }

                // Read 커서 이동 
                if (recvBuffer.OnRead(processLen) == false)
                {
                    Disconnect();
                    return;
                }

                RegisterRecv();
            }
            catch (Exception e)
            {
                Console.WriteLine($"OnRecvCompleted Exception: {e}");
            }
        }
        else
        {
            // TODO Disconnect
            Disconnect();
        }
    }

    #endregion
}

public abstract class PacketSession : Session
{
    // [size][packet][msg..........][size][packet][msg..........]
    public sealed override int OnRecv(ArraySegment<byte> buffer)
    {
        // 수신된 데이터의 hex 덤프 출력 (처음 32바이트만)
        int dumpLength = Math.Min(32, buffer.Count);
        var hexDump = BitConverter.ToString(buffer.Array, buffer.Offset, dumpLength);
        Console.WriteLine($"[DEBUG] 수신 데이터 hex 덤프 ({buffer.Count} bytes): {hexDump}");
        
        int processLen = 0;
        while (true)
        {
            // 최소한 헤더는 파싱할수 있는지 확인
            if (buffer.Count < 2) break;

            // 패킷이 완전체로 도착했는지 확인.
            byte byte1 = buffer.Array[buffer.Offset];
            byte byte2 = buffer.Array[buffer.Offset + 1];
            ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            Console.WriteLine($"[DEBUG] 원시 바이트: [0]={byte1:X2} [1]={byte2:X2}");
            Console.WriteLine($"[DEBUG] BitConverter 결과: {dataSize} (0x{dataSize:X4})");
            Console.WriteLine($"[DEBUG] Little Endian 계산: {(ushort)(byte1 | (byte2 << 8))}");
            Console.WriteLine($"[DEBUG] 수신된 패킷 크기: {dataSize}, 버퍼 크기: {buffer.Count}");
            
            // 비정상적으로 큰 패킷 크기 검증 (최대 64KB로 제한)
            if (dataSize > 65536 || dataSize < 4)
            {
                Console.WriteLine($"[ERROR] 비정상적인 패킷 크기: {dataSize}. 연결을 종료합니다.");
                return -1; // 연결 종료를 위해 음수 반환
            }
            // 적다는 거는 부분적으로만 왔다는 의미
            if (buffer.Count < dataSize)
            {
                Console.WriteLine($"[DEBUG] 패킷 불완전 - 대기 중 (필요: {dataSize}, 현재: {buffer.Count})");
                break;
            }


            // 여기까지 왔으면 패킷 조립 가능.
            Console.WriteLine($"[DEBUG] 패킷 완전 수신 - OnRecvPacket 호출");
            OnRecvPacket(new ArraySegment<byte>(buffer.Array, buffer.Offset, dataSize));

            processLen += dataSize;
            buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + dataSize, buffer.Count - dataSize);
        }

        return processLen;
    }

    public abstract void OnRecvPacket(ArraySegment<byte> buffer);
}

/// <summary>
/// 커스텀 패킷 형식 [size][packetId][protobuf_data]을 처리하는 세션
/// 클라이언트가 사용하는 패킷 형식에 맞춰 처리
/// </summary>
public abstract class CustomPacketSession : Session
{
    public sealed override int OnRecv(ArraySegment<byte> buffer)
    {
        // 수신된 데이터의 hex 덤프 출력 (처음 32바이트만)
        int dumpLength = Math.Min(32, buffer.Count);
        var hexDump = BitConverter.ToString(buffer.Array, buffer.Offset, dumpLength);
        Console.WriteLine($"[DEBUG] CustomPacket 수신 데이터 hex 덤프 ({buffer.Count} bytes): {hexDump}");
        
        int processLen = 0;
        while (true)
        {
            // 최소한 헤더는 파싱할수 있는지 확인 (size: 2bytes + packetId: 2bytes = 4bytes)
            if (buffer.Count < 4) break;

            // 패킷 크기 읽기
            ushort packetSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            Console.WriteLine($"[DEBUG] CustomPacket 크기: {packetSize}");
            
            // 패킷 크기 검증
            if (packetSize > 65536 || packetSize < 4)
            {
                Console.WriteLine($"[ERROR] CustomPacket 비정상적인 패킷 크기: {packetSize}");
                return -1;
            }
            
            // 패킷이 완전히 도착했는지 확인
            if (buffer.Count < packetSize)
            {
                Console.WriteLine($"[DEBUG] CustomPacket 불완전 - 대기 중 (필요: {packetSize}, 현재: {buffer.Count})");
                break;
            }

            // PacketId 읽기
            ushort packetId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);
            Console.WriteLine($"[DEBUG] CustomPacket ID: {packetId}");
            
            // Protobuf 데이터 부분 추출 (size와 packetId 제외)
            int protobufDataSize = packetSize - 4; // 전체 크기에서 헤더 4바이트 제외
            ArraySegment<byte> protobufData = new ArraySegment<byte>(buffer.Array, buffer.Offset + 4, protobufDataSize);
            
            // 패킷 처리
            Console.WriteLine($"[DEBUG] CustomPacket 완전 수신 - OnRecvPacket 호출 (ID: {packetId}, 데이터: {protobufDataSize} bytes)");
            OnRecvPacket(packetId, protobufData);

            processLen += packetSize;
            buffer = new ArraySegment<byte>(buffer.Array, buffer.Offset + packetSize, buffer.Count - packetSize);
        }

        return processLen;
    }

    public abstract void OnRecvPacket(ushort packetId, ArraySegment<byte> protobufData);
}

/// <summary>
/// 순수 Protobuf 데이터를 직접 처리하는 세션 (커스텀 헤더 없음)
/// </summary>
public abstract class ProtobufPacketSession : Session
{
    public sealed override int OnRecv(ArraySegment<byte> buffer)
    {
        // 수신된 데이터의 hex 덤프 출력
        int dumpLength = Math.Min(32, buffer.Count);
        var hexDump = BitConverter.ToString(buffer.Array, buffer.Offset, dumpLength);
        Console.WriteLine($"[DEBUG] ProtobufPacket 수신 데이터 hex 덤프 ({buffer.Count} bytes): {hexDump}");
        
        // 전체 버퍼를 Protobuf 데이터로 처리
        OnRecvProtobuf(buffer);
        
        // 전체 데이터를 처리했다고 반환
        return buffer.Count;
    }

    public abstract void OnRecvProtobuf(ArraySegment<byte> protobufData);
}