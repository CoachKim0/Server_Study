using System.Net;
using System.Net.Sockets;

namespace ServerCore;

/// <summary>
/// 클라이언트 역할을 담당하는 커넥터 클래스
/// 분산 서버 설계시 서버끼리 통신하거나 클라이언트가 서버에 연결할 때 사용
/// TCP 소켓을 이용한 비동기 연결을 처리
/// </summary>
public class Connector
{
    /// <summary>
    /// 세션을 생성하는 팩토리 함수
    /// 연결 성공 시 새로운 세션 인스턴스를 생성하기 위해 사용
    /// </summary>
    Func<Session>? sessionFactory;

    /// <summary>
    /// 지정된 엔드포인트로 비동기 연결을 시작
    /// </summary>
    /// <param name="endPoint">연결할 서버의 IP 주소와 포트</param>
    /// <param name="sessionfactory">연결 성공 시 생성할 세션 팩토리</param>
    public void Connect(IPEndPoint endPoint, Func<Session> sessionfactory)
    {
        
        // TCP 소켓 생성 (스트림 기반 통신용)
        Socket socket = new Socket(
            endPoint.AddressFamily, SocketType.Stream, protocolType: ProtocolType.Tcp);
        this.sessionFactory = sessionfactory;

        // 비동기 연결을 위한 이벤트 설정
        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
        args.Completed += OnConnectComplete;  // 연결 완료 시 호출될 콜백
        args.RemoteEndPoint = endPoint;       // 연결 대상 엔드포인트
        args.UserToken = socket;              // 소켓을 토큰으로 저장

        RegisterConnect(args);
    }

    /// <summary>
    /// 실제 연결 작업을 등록하고 수행
    /// </summary>
    /// <param name="args">연결에 필요한 소켓 정보와 콜백이 담긴 이벤트 인수</param>
    void RegisterConnect(SocketAsyncEventArgs args)
    {
        // UserToken에서 소켓 추출 (안전한 캐스팅)
        Socket? socket = args.UserToken as Socket;
        if (socket == null) 
            return;
        
        // 비동기 연결 시도
        bool pending = socket.ConnectAsync(args);
        // 연결이 즉시 완료된 경우 직접 완료 처리
        if (pending == false)
            OnConnectComplete(null!, args);
    }

    /// <summary>
    /// 연결 완료 시 호출되는 콜백 함수
    /// 연결 성공 시 세션을 생성하고 시작, 실패 시 에러 로그 출력
    /// </summary>
    /// <param name="sender">이벤트 발생자 (null일 수 있음)</param>
    /// <param name="args">연결 결과 정보가 담긴 이벤트 인수</param>
    void OnConnectComplete(object? sender, SocketAsyncEventArgs args)
    {
        if (args.SocketError == SocketError.Success)
        {
            // 연결 성공: 새 세션 생성 및 시작
            Session session = sessionFactory!.Invoke();
            session.Start(args.ConnectSocket!);
            session.OnConnected(args.RemoteEndPoint!);
        }
        else
        {
            // 연결 실패: 에러 로그 출력
            Console.WriteLine($"OnConnectComplete Failed : {args.SocketError}");
        }
    }
}