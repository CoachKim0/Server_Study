using System.Net;
using System.Text;
using Server_Study;
using ServerCore;



/// <summary>
/// 게임 서버의 메인 프로그램 클래스
/// 서버 초기화 및 클라이언트 연결 수락을 담당
/// </summary>
class Program
{
    /// <summary>
    /// 클라이언트 연결을 수락하는 리스너 인스턴스
    /// </summary>
    static Listener listener = new Listener();

    /// <summary>
    /// 프로그램의 진입점
    /// 서버를 초기화하고 클라이언트 연결을 대기
    /// </summary>
    /// <param name="args">명령행 인수</param>
    static void Main(string[] args)
    {
        // 현재 컴퓨터의 호스트명 가져오기
        string host = Dns.GetHostName();
        
        // 호스트의 IP 정보 조회
        IPHostEntry ipHost = Dns.GetHostEntry(host);
        
        // 첫 번째 IP 주소 사용 (일반적으로 로컬 IP)
        IPAddress ipAddr = ipHost.AddressList[0];
        
        // 서버 바인딩용 엔드포인트 생성 (포트 7777 사용)
        IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);

        // 리스너 초기화: 지정된 엔드포인트에서 연결 대기
        // 클라이언트 연결 시 GameSession 인스턴스를 생성하는 팩토리 함수 제공
        listener.Init(
            endPoint,
            () => { return new ClientSession(); }
        );

        Console.WriteLine("Listening...");
        
        // 서버를 계속 실행 상태로 유지 (무한 루프)
        while (true)
        {
            // 메인 스레드는 대기 상태 유지
            // 실제 클라이언트 처리는 백그라운드 스레드에서 수행
        }
    }
}