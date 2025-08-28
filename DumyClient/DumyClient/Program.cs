using DummyClient.Core.NetworkManagers;
using DummyClient.Tests;
using DummyClient.Chat.Tests;
using DummyClient.Chat.Common;

namespace DummyClient;

/// <summary>
/// 메인 프로그램
/// - TCP와 gRPC 기능이 깔끔하게 분리된 새로운 구조 데모
/// - 새로운 Chat 폴더 기반 채팅 시스템 통합
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== DummyClient - 정리된 구조 데모 ===");
        
        while (true)
        {
            Console.WriteLine("\n🔹 네트워크 매니저 테스트:");
            Console.WriteLine("1: TCP 네트워크 테스트");
            Console.WriteLine("2: gRPC 네트워크 테스트"); 
            Console.WriteLine("3: 네트워크 테스트 둘 다");
            Console.WriteLine("4: TCP 채팅 매니저 테스트");
            Console.WriteLine("5: gRPC 채팅 매니저 테스트");
            Console.WriteLine("6: 채팅 매니저 테스트 둘 다");
            Console.WriteLine("7: TCP 인터랙티브 채팅 (실시간)");
            Console.WriteLine("8: gRPC 인터랙티브 채팅 (실시간)");
            
            Console.WriteLine("\n🔸 새로운 Chat 서비스 테스트:");
            Console.WriteLine("11: TCP 채팅 서비스 기본 테스트");
            Console.WriteLine("12: gRPC 채팅 서비스 기본 테스트");
            Console.WriteLine("13: TCP 채팅 서비스 대화형 테스트");
            Console.WriteLine("14: gRPC 채팅 서비스 대화형 테스트");
            Console.WriteLine("15: 모든 채팅 서비스 기본 테스트");
            
            Console.WriteLine("\n0: 종료");
            Console.Write("\n선택하세요: ");
            
            var choice = Console.ReadLine();
            
            try 
            {
                switch (choice)
                {
                    case "1":
                        await NetworkManagerTests.TestTcpNetworkManager();
                        break;
                    case "2":
                        await NetworkManagerTests.TestGrpcNetworkManager();
                        break;
                    case "3":
                        await NetworkManagerTests.TestTcpNetworkManager();
                        Console.WriteLine();
                        await NetworkManagerTests.TestGrpcNetworkManager();
                        break;
                    case "4":
                        await ChatManagerTests.TestTcpChatManager();
                        break;
                    case "5":
                        await ChatManagerTests.TestGrpcChatManager();
                        break;
                    case "6":
                        await ChatManagerTests.TestTcpChatManager();
                        Console.WriteLine();
                        await ChatManagerTests.TestGrpcChatManager();
                        break;
                    case "7":
                        await ChatManagerTests.InteractiveChatTest(NetworkManagerFactory.NetworkType.TCP);
                        break;
                    case "8":
                        await ChatManagerTests.InteractiveChatTest(NetworkManagerFactory.NetworkType.gRPC);
                        break;
                    // 새로운 Chat 서비스 테스트
                    case "11":
                        await ChatServiceTests.BasicChatTest(ChatServiceFactory.ProtocolType.TCP);
                        break;
                    case "12":
                        await ChatServiceTests.BasicChatTest(ChatServiceFactory.ProtocolType.gRPC);
                        break;
                    case "13":
                        await ChatServiceTests.InteractiveChatTest(ChatServiceFactory.ProtocolType.TCP);
                        break;
                    case "14":
                        await ChatServiceTests.InteractiveChatTest(ChatServiceFactory.ProtocolType.gRPC);
                        break;
                    case "15":
                        await ChatServiceTests.RunAllTests();
                        break;
                    case "0":
                        Console.WriteLine("프로그램을 종료합니다.");
                        return;
                    default:
                        Console.WriteLine("잘못된 선택입니다.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 오류 발생: {ex.Message}");
            }
            
            Console.WriteLine("\n아무 키나 누르면 메뉴로 돌아갑니다...");
            Console.ReadKey();
            Console.Clear();
        }
    }
}