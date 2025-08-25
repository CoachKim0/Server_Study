using GamePackets;
using Google.Protobuf;

Console.WriteLine("=== Protocol Buffers 패킷 사용 예제 ===");
Console.WriteLine();

// 1. HelloPacket 기본 사용법
Console.WriteLine("1. HelloPacket 기본 사용:");
var helloPacket = new HelloPacket
{
    Message = "안녕하세요! 첫 번째 Protobuf 패킷입니다."
};
Console.WriteLine($"   생성된 메시지: {helloPacket.Message}");

// 직렬화 (객체 -> 바이너리)
byte[] helloData = helloPacket.ToByteArray();
Console.WriteLine($"   직렬화된 데이터 크기: {helloData.Length} bytes");
Console.WriteLine($"   바이너리 데이터: {Convert.ToHexString(helloData)}");

// 역직렬화 (바이너리 -> 객체)
var restoredHello = HelloPacket.Parser.ParseFrom(helloData);
Console.WriteLine($"   복원된 메시지: {restoredHello.Message}");
Console.WriteLine($"   패킷이 동일한가? {helloPacket.Equals(restoredHello)}");
Console.WriteLine();

// 2. PlayerInfoReq 패킷 사용 (기존 XML PDL과 동일한 구조)
Console.WriteLine("2. PlayerInfoReq 패킷 사용:");
var playerReq = new PlayerInfoReq
{
    PlayerId = 12345,
    Name = "테스트플레이어"
};
Console.WriteLine($"   플레이어 ID: {playerReq.PlayerId}");
Console.WriteLine($"   플레이어 이름: {playerReq.Name}");

byte[] playerReqData = playerReq.ToByteArray();
Console.WriteLine($"   직렬화 크기: {playerReqData.Length} bytes");

// 복원
var restoredPlayerReq = PlayerInfoReq.Parser.ParseFrom(playerReqData);
Console.WriteLine($"   복원된 ID: {restoredPlayerReq.PlayerId}");
Console.WriteLine($"   복원된 이름: {restoredPlayerReq.Name}");
Console.WriteLine();

// 4. ChatMessage 패킷 사용
Console.WriteLine("4. ChatMessage 패킷 사용:");
var chatMsg = new ChatMessage
{
    Sender = "플레이어1",
    Message = "안녕하세요! 채팅 테스트입니다.",
    Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
};
Console.WriteLine($"   발신자: {chatMsg.Sender}");
Console.WriteLine($"   메시지: {chatMsg.Message}");
Console.WriteLine($"   시간: {DateTimeOffset.FromUnixTimeSeconds(chatMsg.Timestamp)}");

byte[] chatData = chatMsg.ToByteArray();
Console.WriteLine($"   직렬화 크기: {chatData.Length} bytes");
Console.WriteLine();

// 5. XML vs Protobuf 비교 (교육용)
Console.WriteLine("5. XML vs Protobuf 비교:");
Console.WriteLine("   XML 장점: 사람이 읽기 쉬움, 디버깅 편리");
Console.WriteLine("   XML 단점: 크기가 큼, 파싱 속도 느림");
Console.WriteLine("   Protobuf 장점: 작은 크기, 빠른 속도, 타입 안전성");
Console.WriteLine("   Protobuf 단점: 바이너리라 사람이 읽기 어려움");
Console.WriteLine();

Console.WriteLine("=== 패킷 테스트 완료 ===");
Console.WriteLine("빌드가 성공했다면 .proto 파일에서 C# 클래스가 자동 생성된 것입니다!");