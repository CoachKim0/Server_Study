using DummyClient.Core.NetworkManagers;
using DummyClient.Tests;
using DummyClient.Chat.Tests;
using DummyClient.Chat.Common;

namespace DummyClient;

/// <summary>
/// 메인 프로그램
/// - TCP Protocol Buffers 채팅 테스트
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== DummyClient - TCP 채팅 Protocol Buffers 테스트 ===");
        
        // 자동 TCP 채팅 테스트 실행
        await TestTcpChat.RunTest();
    }
}