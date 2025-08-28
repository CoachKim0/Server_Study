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
    Socket? socket;

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
    /// JSON 형태의 데이터를 수신했을 때 호출되는 콜백 함수
    /// 하위 클래스에서 JSON 패킷 처리 로직을 구현
    /// </summary>
    /// <param name="jsonData">수신된 JSON 문자열</param>
    public abstract void OnRecvJsonPacket(string jsonData);


    /// <summary>
    /// 세션을 시작하고 수신 대기 상태로 전환
    /// 비동기 이벤트 핸들러를 등록하고 수신를 시작
    /// </summary>
    /// <param name="socket">클라이언트와 연결된 소켓</param>
    public void Start(Socket socket)
    {
        this.socket = socket;
        // 비동기 수신/송신 이벤트 핸들러 등록
        recvArgs.Completed += OnRecvCompleted;
        sendArgs.Completed += OnSendCompleted;

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
        if (Interlocked.Exchange(ref _disconnect, 1) == 1)
            return;

        OnDisconnected(socket!.RemoteEndPoint!);
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

        bool pending = socket!.SendAsync(sendArgs);
        if (pending == false)
            OnSendCompleted(null!, sendArgs);
    }

    void OnSendCompleted(object? sender, SocketAsyncEventArgs args)
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


        bool pending = socket!.ReceiveAsync(recvArgs);
        if (pending == false) // 바로 성공했을 경우
            OnRecvCompleted(null!, recvArgs);
    }

    void OnRecvCompleted(object? sender, SocketAsyncEventArgs args)
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

/// <summary>
/// 순수 Protobuf 데이터를 직접 처리하는 세션 (커스텀 헤더 없음)
/// </summary>
public abstract class ProtobufPacketSession : Session
{
    public sealed override int OnRecv(ArraySegment<byte> buffer)
    {
        // 수신된 데이터의 hex 덤프 출력
        int dumpLength = Math.Min(32, buffer.Count);
        var hexDump = BitConverter.ToString(buffer.Array!, buffer.Offset, dumpLength);
        Console.WriteLine($"[DEBUG] ProtobufPacket 수신 데이터 hex 덤프 ({buffer.Count} bytes): {hexDump}");
        
        // 전체 버퍼를 Protobuf 데이터로 처리
        OnRecvProtobuf(buffer);
        
        // 전체 데이터를 처리했다고 반환
        return buffer.Count;
    }

    public abstract void OnRecvProtobuf(ArraySegment<byte> protobufData);
    
    public override void OnRecvJsonPacket(string jsonData)
    {
        Console.WriteLine($"[INFO] ProtobufPacketSession에서 JSON 패킷 수신: {jsonData}");
        // 기본적으로 JSON 패킷은 처리하지 않음 (필요시 하위 클래스에서 오버라이드)
    }
}

/// <summary>
/// 커스텀 패킷 형식 [PacketID][Protobuf데이터]를 처리하는 세션
/// 클라이언트가 PacketID + Protobuf 형식으로 전송하는 패킷을 처리
/// </summary>
public abstract class CustomPacketSession : Session
{
    public sealed override int OnRecv(ArraySegment<byte> buffer)
    {
        // 수신된 데이터의 hex 덤프 출력
        int dumpLength = Math.Min(32, buffer.Count);
        var hexDump = BitConverter.ToString(buffer.Array!, buffer.Offset, dumpLength);
        Console.WriteLine($"[DEBUG] CustomPacket 수신 데이터 hex 덤프 ({buffer.Count} bytes): {hexDump}");
        
        int processLen = 0;
        while (true)
        {
            // 최소 데이터 크기 확인
            if (buffer.Count < 1) break;

            // JSON 데이터인지 확인 ('{' 문자로 시작)
            if (buffer.Array![buffer.Offset] == 0x7B) // '{'
            {
                // JSON 데이터 처리
                string jsonString = System.Text.Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
                Console.WriteLine($"[DEBUG] JSON 데이터 수신: {jsonString}");
                
                // JSON 데이터를 처리하고 전체 버퍼 소비
                OnRecvJsonPacket(jsonString);
                processLen = buffer.Count;
                break;
            }
            
            // 기존 바이너리 패킷 처리
            // 최소한 PacketID는 파싱할 수 있는지 확인 (2바이트)
            if (buffer.Count < 2) break;

            // PacketID 읽기 (Little Endian)
            ushort packetId = BitConverter.ToUInt16(buffer.Array!, buffer.Offset);
            Console.WriteLine($"[DEBUG] CustomPacket ID: {packetId}");
            
            // 패킷 ID 검증 (필요시)
            if (packetId > 1000) // 비정상적으로 큰 PacketID
            {
                Console.WriteLine($"[ERROR] CustomPacket 비정상적인 PacketID: {packetId}");
                return -1;
            }
            
            // Protobuf 데이터 부분 추출 (PacketID 2바이트 제외)
            int protobufDataSize = buffer.Count - 2;
            if (protobufDataSize < 0)
            {
                Console.WriteLine($"[ERROR] CustomPacket Protobuf 데이터 크기 음수: {protobufDataSize}");
                break;
            }
            
            ArraySegment<byte> protobufData = new ArraySegment<byte>(buffer.Array, buffer.Offset + 2, protobufDataSize);
            
            // 패킷 처리
            Console.WriteLine($"[DEBUG] CustomPacket 처리 - PacketID: {packetId}, Protobuf 데이터: {protobufDataSize} bytes");
            OnRecvPacket(packetId, protobufData);

            // 전체 데이터를 처리했다고 반환 (한 번에 하나의 패킷만 처리)
            processLen = buffer.Count;
            break;
        }

        return processLen;
    }

    public abstract void OnRecvPacket(ushort packetId, ArraySegment<byte> protobufData);
    public abstract override void OnRecvJsonPacket(string jsonData);
}