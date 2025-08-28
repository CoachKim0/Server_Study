using DummyClient.Chat.Interfaces;

namespace DummyClient.Chat.Common;

/// <summary>
/// 채팅 서비스 팩토리
/// 프로토콜 타입에 따라 적절한 채팅 서비스를 생성합니다
/// </summary>
public static class ChatServiceFactory
{
    public enum ProtocolType
    {
        TCP,
        gRPC
    }

    public static IChatService CreateChatService(ProtocolType protocolType)
    {
        return protocolType switch
        {
            ProtocolType.TCP => new TCP.TcpChatService(),
            ProtocolType.gRPC => new gRPC.GrpcChatService(),
            _ => throw new ArgumentException($"Unsupported protocol type: {protocolType}")
        };
    }
}