using DummyClient.Core.Interfaces;
using DummyClient.Core.NetworkManagers;

namespace DummyClient.Tests;

/// <summary>
/// 네트워크 매니저 테스트
/// </summary>
public static class NetworkManagerTests
{
    public static async Task TestTcpNetworkManager()
    {
        Console.WriteLine("=== TCP Network Manager Test ===");
        
        var tcpManager = NetworkManagerFactory.CreateNetworkManager(NetworkManagerFactory.NetworkType.TCP);
        
        Console.WriteLine($"Initial connection status: {tcpManager.IsConnected}");
        
        await tcpManager.ConnectAsync("localhost", 7777);
        Console.WriteLine($"After connect: {tcpManager.IsConnected}");
        
        await tcpManager.SendMessageAsync("Hello TCP!");
        
        await tcpManager.DisconnectAsync();
        Console.WriteLine($"After disconnect: {tcpManager.IsConnected}");
    }
    
    public static async Task TestGrpcNetworkManager()
    {
        Console.WriteLine("=== gRPC Network Manager Test ===");
        
        var grpcManager = NetworkManagerFactory.CreateNetworkManager(NetworkManagerFactory.NetworkType.gRPC);
        
        Console.WriteLine($"Initial connection status: {grpcManager.IsConnected}");
        
        await grpcManager.ConnectAsync("localhost", 5554);
        Console.WriteLine($"After connect: {grpcManager.IsConnected}");
        
        await grpcManager.SendMessageAsync("Hello gRPC!");
        
        await grpcManager.DisconnectAsync();
        Console.WriteLine($"After disconnect: {grpcManager.IsConnected}");
    }
}