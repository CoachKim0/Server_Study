using DummyClient.Core.Interfaces;

namespace DummyClient.Core.NetworkManagers;

/// <summary>
/// 네트워크 타입에 따라 적절한 매니저를 생성하는 팩토리
/// </summary>
public static class NetworkManagerFactory
{
    public enum NetworkType
    {
        TCP,
        gRPC
    }

    public static INetworkManager CreateNetworkManager(NetworkType type)
    {
        return type switch
        {
            NetworkType.TCP => new TCP.TcpNetworkManager(),
            NetworkType.gRPC => new gRPC.GrpcNetworkManager(),
            _ => throw new ArgumentException($"Unsupported network type: {type}")
        };
    }

    public static IChatManager CreateChatManager(NetworkType type)
    {
        return type switch
        {
            NetworkType.TCP => new TCP.TcpChatManager(),
            NetworkType.gRPC => new gRPC.GrpcChatManager(), 
            _ => throw new ArgumentException($"Unsupported network type: {type}")
        };
    }
}