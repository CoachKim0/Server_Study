using System.Net;
using Server_Study;
using ServerCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Server.Grpc.Services;
using Server_Study.Modules.Common.ChatBase;
using Server_Study.Modules.Auth;
using Server_Study.Modules.Room;
using Server_Study.Modules.Ping;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Server_Study.Modules.GamePlay.Room;
using Server_Study.Services;

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
    
    public static GameRoom room = new GameRoom();

    /// <summary>
    /// 프로그램의 진입점
    /// 서버를 초기화하고 클라이언트 연결을 대기
    /// </summary>
    /// <param name="args">명령행 인수</param>
    static async Task Main(string[] args)
    {
        // gRPC 서버 설정
        var builder = WebApplication.CreateBuilder(args);
        
        // gRPC 서비스 추가
        builder.Services.AddGrpc();
        
        // 핸들러들 등록
        builder.Services.AddScoped<IAuthHandler, AuthHandler>();
        builder.Services.AddScoped<IRoomHandler, RoomHandler>();
        builder.Services.AddScoped<IPingHandler, PingHandler>();
        
        // BroadcastService 등록 (별도 서비스)
        builder.Services.AddScoped<IBroadcastService, BroadcastService>();
        
        // Kestrel 서버 설정 - gRPC용 포트 5554
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenAnyIP(5554, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        var app = builder.Build();
        app.MapGrpcService<GameGrpcService>();
        app.MapGrpcService<ChatServiceImpl>();

        // gRPC 서버를 백그라운드에서 실행
        var grpcServerTask = Task.Run(() => app.RunAsync());
        
        Console.WriteLine("gRPC 서버 시작됨 (포트: 5554)");

        // JobQueue 성능 테스트 실행
        JobQueuePerformanceTest.RunBenchmark();

        // 기존 소켓 서버 초기화 (포트 7777)
        string host = Dns.GetHostName();
        IPHostEntry ipHost = Dns.GetHostEntry(host);
        IPAddress ipAddr = ipHost.AddressList[0];
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 7777);

        try
        {
            listener.Init(endPoint, () => { return Session_Manager.Instance.Generate(); });
            Console.WriteLine("소켓 서버 초기화 성공! (포트: 7777)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"소켓 서버 초기화 실패: {ex.Message}");
        }

        Console.WriteLine("두 서버 모두 실행 중...");
        Console.WriteLine("- 소켓 서버: 포트 7777");
        Console.WriteLine("- gRPC 서버: 포트 5554");
        
        // JobQueue 통계 모니터링 시작
        var monitoringTask = Task.Run(async () => {
            while (true)
            {
                await Task.Delay(10000); // 10초마다 통계 출력
                
                var stats = ServerCore.JobQueueManager.Instance.GetQueueStats();
                if (stats.Count > 0)
                {
                    Console.WriteLine("=== JobQueue 통계 ===");
                    foreach (var stat in stats)
                    {
                        Console.WriteLine($"Queue '{stat.Key}': {stat.Value} jobs pending");
                    }
                    Console.WriteLine("=====================");
                }
            }
        });
        
        // 서버를 계속 실행 상태로 유지
        await Task.Delay(-1);
    }
}