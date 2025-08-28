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
        Console.WriteLine("=== DummyClient 테스트 프로그램 ===");
        Console.WriteLine("1. 인증 시스템 테스트");
        Console.WriteLine("2. TCP 채팅 테스트");
        Console.Write("선택하세요 (1-2): ");

        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await TestAuthClient.RunAuthTests();
                break;
            case "2":
                await TestTcpChat.RunTest();
                break;
            default:
                Console.WriteLine("잘못된 선택입니다. 인증 테스트를 실행합니다.");
                await TestAuthClient.RunAuthTests();
                break;
        }
    }
}