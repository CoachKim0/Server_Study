using System.Net;
using System.Net.Sockets;

namespace ServerCore;

/// <summary>
/// 서버의 연결 수락을 담당하는 리스너 클래스
/// 클라이언트의 연결 요청을 비동기적으로 처리하고 세션을 생성
/// TCP 소켓을 이용한 서버 측 연결 관리
/// </summary>
public class Listener
{
    /// <summary>
    /// 클라이언트 연결을 대기하는 서버 소켓
    /// </summary>
    Socket? _listenSocket;
    
    /// <summary>
    /// 클라이언트 연결 시 새로운 세션을 생성하는 팩토리 함수
    /// </summary>
    Func<Session>? sessionFactory;


    /// <summary>
    /// 리스너를 초기화하고 클라이언트 연결 대기를 시작
    /// </summary>
    /// <param name="endPoint">바인딩할 IP 주소와 포트</param>
    /// <param name="sessionfactory">연결된 클라이언트를 위한 세션 생성 팩토리</param>
    public void Init(IPEndPoint endPoint, Func<Session>  sessionfactory)
    {
        // TCP 소켓 생성 (서버용 리슨 소켓)
        _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        sessionFactory += sessionfactory;

        // 지정된 엔드포인트에 소켓 바인딩
        _listenSocket.Bind(endPoint);

        // 연결 대기 시작 (backlog: 최대 대기 큐 크기 = 10)
        _listenSocket.Listen(10);

        // 비동기 Accept를 위한 이벤트 설정
        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
        args.Completed += OnAcceptComplete;
        RegisterAccept(args);
    }

    /// <summary>
    /// 클라이언트 연결 수락을 비동기적으로 등록
    /// </summary>
    /// <param name="args">Accept 작업에 사용할 이벤트 인수</param>
    void RegisterAccept(SocketAsyncEventArgs args)
    {
        // 이전 연결 소켓 정보 초기화
        args.AcceptSocket = null;
        
        // 비동기 Accept 시작
        bool pending = _listenSocket!.AcceptAsync(args);
        
        // 즉시 연결이 완료된 경우 직접 완료 처리
        if (pending == false)
            OnAcceptComplete(null!, args);
    }


    /// <summary>
    /// 클라이언트 연결 수락 완료 시 호출되는 콜백 함수
    /// 멀티스레드 환경에서 실행될 수 있으므로 스레드 안전성을 고려해야 함
    /// </summary>
    /// <param name="sender">이벤트 발생자 (null일 수 있음)</param>
    /// <param name="args">Accept 결과 정보가 담긴 이벤트 인수</param>
    void OnAcceptComplete(object? sender, SocketAsyncEventArgs args)
    {
        if (args.SocketError == SocketError.Success)
        {
            // 연결 성공: 새 세션 생성 및 시작
            Session session = sessionFactory!.Invoke();
            session.Start(args.AcceptSocket!);
            session.OnConnected(args.AcceptSocket!.RemoteEndPoint!);
        }
        else
        {
            // 연결 실패: 에러 로그 출력
            Console.WriteLine(args.SocketError.ToString());
        }

        // 다음 연결을 위해 다시 Accept 등록
        RegisterAccept(args);
    }
}