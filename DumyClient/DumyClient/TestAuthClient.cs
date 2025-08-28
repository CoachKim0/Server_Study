using Grpc.Net.Client;
using GrpcApp;

namespace DummyClient;

/// <summary>
/// 인증 테스트 클라이언트
/// 1. 인증 기본 테스트
/// 2. 회원가입 테스트
/// 3. 로그인 테스트
/// </summary>
public class TestAuthClient
{
    private GrpcChannel? _channel;
    private GameService.GameServiceClient? _client;

    public async Task<bool> ConnectAsync(string serverAddress, int port)
    {
        try
        {
            var address = $"http://{serverAddress}:{port}";
            _channel = GrpcChannel.ForAddress(address);
            _client = new GameService.GameServiceClient(_channel);
            
            Console.WriteLine($"✅ 서버 연결 성공: {address}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 서버 연결 실패: {ex.Message}");
            return false;
        }
    }

    public async Task TestAuthentication()
    {
        if (_client == null)
        {
            Console.WriteLine("❌ 서버에 연결되지 않았습니다");
            return;
        }

        Console.WriteLine("\n=== 1. 인증 테스트 ===");

        try
        {
            var request = new GameMessage
            {
                UserId = "testuser",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AuthUser = new AuthUser
                {
                    PlatformType = 1, // 로그인
                    AuthKey = "testuser",
                    RetPassKey = "password123"
                }
            };

            using var call = _client.Game();
            await call.RequestStream.WriteAsync(request);
            
            // 응답 대기 (간단한 타임아웃)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            if (await call.ResponseStream.MoveNext(cts.Token))
            {
                var response = call.ResponseStream.Current;
                Console.WriteLine($"✅ 인증 응답:");
                Console.WriteLine($"   ResultCode: {response.ResultCode}");
                Console.WriteLine($"   Message: {response.ResultMessage}");
                Console.WriteLine($"   Token: {response.Token}");
            }
            else
            {
                Console.WriteLine("❌ 응답을 받지 못했습니다");
            }

            // 스트림 완료는 응답을 받은 후에 처리
            await call.RequestStream.CompleteAsync();
            await Task.Delay(100); // 응답 처리 대기
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 인증 테스트 실패: {ex.Message}");
        }
    }

    public async Task TestRegistration()
    {
        if (_client == null)
        {
            Console.WriteLine("❌ 서버에 연결되지 않았습니다");
            return;
        }

        Console.WriteLine("\n=== 2. 회원가입 테스트 ===");

        try
        {
            var request = new GameMessage
            {
                UserId = "newuser",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AuthUser = new AuthUser
                {
                    PlatformType = 2, // 회원가입
                    AuthKey = "newuser",
                    RetPassKey = "newpass123"
                }
            };

            using var call = _client.Game();
            await call.RequestStream.WriteAsync(request);
            
            // 응답 대기 (간단한 타임아웃)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            if (await call.ResponseStream.MoveNext(cts.Token))
            {
                var response = call.ResponseStream.Current;
                Console.WriteLine($"✅ 회원가입 응답:");
                Console.WriteLine($"   ResultCode: {response.ResultCode}");
                Console.WriteLine($"   Message: {response.ResultMessage}");
                Console.WriteLine($"   Token: {response.Token}");
            }
            else
            {
                Console.WriteLine("❌ 응답을 받지 못했습니다");
            }

            // 스트림 완료는 응답을 받은 후에 처리
            await call.RequestStream.CompleteAsync();
            await Task.Delay(100); // 응답 처리 대기
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 회원가입 테스트 실패: {ex.Message}");
        }
    }

    public async Task TestLogin()
    {
        if (_client == null)
        {
            Console.WriteLine("❌ 서버에 연결되지 않았습니다");
            return;
        }

        Console.WriteLine("\n=== 3. 로그인 테스트 ===");

        try
        {
            var request = new GameMessage
            {
                UserId = "admin",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AuthUser = new AuthUser
                {
                    PlatformType = 1, // 로그인
                    AuthKey = "admin",
                    RetPassKey = "admin123"
                }
            };

            using var call = _client.Game();
            await call.RequestStream.WriteAsync(request);
            
            // 응답 대기 (간단한 타임아웃)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            if (await call.ResponseStream.MoveNext(cts.Token))
            {
                var response = call.ResponseStream.Current;
                Console.WriteLine($"✅ 로그인 응답:");
                Console.WriteLine($"   ResultCode: {response.ResultCode}");
                Console.WriteLine($"   Message: {response.ResultMessage}");
                Console.WriteLine($"   Token: {response.Token}");
            }
            else
            {
                Console.WriteLine("❌ 응답을 받지 못했습니다");
            }

            // 스트림 완료는 응답을 받은 후에 처리
            await call.RequestStream.CompleteAsync();
            await Task.Delay(100); // 응답 처리 대기
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 로그인 테스트 실패: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        _channel?.Dispose();
        _channel = null;
        _client = null;
        Console.WriteLine("🔌 연결 해제됨");
    }

    public static async Task RunAuthTests()
    {
        Console.WriteLine("=== 인증 시스템 테스트 시작 ===");
        
        var authClient = new TestAuthClient();
        
        try
        {
            // 서버 연결
            bool connected = await authClient.ConnectAsync("127.0.0.1", 5554);
            if (!connected)
            {
                Console.WriteLine("❌ 서버 연결 실패로 테스트 중단");
                return;
            }

            // 1. 인증 테스트
            await authClient.TestAuthentication();
            await Task.Delay(1000);

            // 2. 회원가입 테스트
            await authClient.TestRegistration();
            await Task.Delay(1000);

            // 3. 로그인 테스트
            await authClient.TestLogin();
            await Task.Delay(1000);

            Console.WriteLine("\n=== 모든 테스트 완료 ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 테스트 중 오류 발생: {ex.Message}");
        }
        finally
        {
            authClient.Disconnect();
        }
    }
}