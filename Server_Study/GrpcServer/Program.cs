using GrpcServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Kestrel을 HTTP/2용으로 설정
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5554, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// gRPC 서비스 추가
builder.Services.AddGrpc();

// CORS 정책 추가 (웹 클라이언트 지원용)
builder.Services.AddCors(o => o.AddPolicy("AllowAll", builder =>
{
    builder.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader()
           .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
}));

var app = builder.Build();

// gRPC-Web 지원 (웹 클라이언트용)
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
app.UseCors("AllowAll");

// gRPC 서비스 매핑
app.MapGrpcService<GameGrpcService>();

// 기본 응답
app.MapGet("/", () => "gRPC GameService Server is running on port 5554");

Console.WriteLine("gRPC 서버가 포트 5554에서 시작되었습니다.");
app.Run();