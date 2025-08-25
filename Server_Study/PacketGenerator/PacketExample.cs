using System;
using GamePackets;
using Google.Protobuf;

namespace PacketGenerator
{
    public class PacketExample
    {
        public static void RunExamples()
        {
            Console.WriteLine("=== Protocol Buffers 패킷 사용 예제 ===\n");
            
            // 1. ChatMessage 패킷 예제
            ChatMessageExample();
            
            // 2. HelloPacket 예제
            HelloPacketExample();
            
            // 3. PlayerInfoReq 복잡한 패킷 예제 (실제 gamepacket.proto에서 생성됨!)
            PlayerInfoReqExample();
            
            // 4. 네트워크 전송 시뮬레이션
            NetworkExample();
            
            // 5. PacketID enum 사용 예제
            PacketIDExample();
            
            // 6. 복잡한 패킷 구조 설명
            Console.WriteLine("=== 복잡한 패킷 구조 설명 ===");
            ShowComplexPacketExample();
        }

        static void ChatMessageExample()
        {
            Console.WriteLine("--- ChatMessage 예제 ---");
            
            // 기존 수동 방식과 비교
            Console.WriteLine("기존 수동 방식 (DummyClient 스타일):");
            Console.WriteLine("class ChatMessage : Packet");
            Console.WriteLine("{");
            Console.WriteLine("    public string sender;");
            Console.WriteLine("    public string message;");
            Console.WriteLine("    public long timestamp;");
            Console.WriteLine("    ");
            Console.WriteLine("    public override ArraySegment<byte> Write()");
            Console.WriteLine("    {");
            Console.WriteLine("        // 수동으로 바이트 위치 계산");
            Console.WriteLine("        // sender 길이(2) + sender + message 길이(2) + message + timestamp(8)");
            Console.WriteLine("        // 복잡한 직렬화 코드...");
            Console.WriteLine("    }");
            Console.WriteLine("}");
            Console.WriteLine();
            
            Console.WriteLine("Protocol Buffers 방식:");
            
            var chatMsg = new ChatMessage();
            chatMsg.Sender = "Player123";
            chatMsg.Message = "안녕하세요! Protocol Buffers 테스트입니다.";
            chatMsg.Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            
            Console.WriteLine($"var chatMsg = new ChatMessage();");
            Console.WriteLine($"chatMsg.Sender = \"{chatMsg.Sender}\";");
            Console.WriteLine($"chatMsg.Message = \"{chatMsg.Message}\";");
            Console.WriteLine($"chatMsg.Timestamp = {chatMsg.Timestamp};");
            Console.WriteLine();
            
            Console.WriteLine($"채팅 메시지: [{chatMsg.Sender}] {chatMsg.Message}");
            Console.WriteLine($"타임스탬프: {chatMsg.Timestamp} ({DateTimeOffset.FromUnixTimeSeconds(chatMsg.Timestamp)})");
            
            // 전송/수신
            byte[] data = chatMsg.ToByteArray();
            Console.WriteLine($"byte[] data = chatMsg.ToByteArray(); // 자동 직렬화, {data.Length}바이트");
            
            var receivedChat = ChatMessage.Parser.ParseFrom(data);
            Console.WriteLine($"var receivedChat = ChatMessage.Parser.ParseFrom(data); // 자동 역직렬화");
            Console.WriteLine($"수신된 채팅: [{receivedChat.Sender}] {receivedChat.Message}");
            Console.WriteLine();
        }

        static void HelloPacketExample()
        {
            Console.WriteLine("--- HelloPacket 예제 ---");
            
            var hello = new HelloPacket();
            hello.Message = "Hello, Protocol Buffers!";
            
            Console.WriteLine($"var hello = new HelloPacket();");
            Console.WriteLine($"hello.Message = \"{hello.Message}\";");
            
            byte[] data = hello.ToByteArray();
            Console.WriteLine($"전송 데이터 크기: {data.Length}바이트");
            
            var received = HelloPacket.Parser.ParseFrom(data);
            Console.WriteLine($"받은 메시지: {received.Message}");
            Console.WriteLine();
        }

        static void PlayerInfoReqExample()
        {
            Console.WriteLine("--- PlayerInfoReq 복잡한 패킷 예제 (gamepacket.proto) ---");
            
            // 사용자가 보여준 예제와 동일한 구조!
            Console.WriteLine("기존 수동 방식:");
            Console.WriteLine("PlayerInfoReq packet = new PlayerInfoReq()");
            Console.WriteLine("{ playerId = 1001, name = \"ABCD\" };");
            Console.WriteLine("packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 101, level = 1, duration = 1.0f });");
            Console.WriteLine("// ... 더 많은 스킬들");
            Console.WriteLine("ArraySegment<byte> s = packet.Write(); // 복잡한 수동 직렬화");
            Console.WriteLine();
            
            Console.WriteLine("Protocol Buffers 방식:");
            
            // 실제 gamepacket.proto에서 생성된 클래스 사용!
            var packet = new PlayerInfoReq();
            packet.PlayerId = 1001;
            packet.Name = "ABCD";
            
            // 스킬 추가 (사용자 예제와 거의 동일!)
            packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 101, Level = 1, Duration = 1.0f });
            packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 201, Level = 2, Duration = 2.0f });
            packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 301, Level = 3, Duration = 3.0f });
            packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 401, Level = 4, Duration = 4.0f });
            
            Console.WriteLine($"var packet = new PlayerInfoReq();");
            Console.WriteLine($"packet.PlayerId = {packet.PlayerId};");
            Console.WriteLine($"packet.Name = \"{packet.Name}\";");
            Console.WriteLine();
            Console.WriteLine("// 스킬 추가");
            foreach (var skill in packet.Skills)
            {
                Console.WriteLine($"packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo {{ Id = {skill.Id}, Level = {skill.Level}, Duration = {skill.Duration}f }});");
            }
            Console.WriteLine();
            
            // 직렬화 (자동!)
            byte[] data = packet.ToByteArray();
            Console.WriteLine($"byte[] data = packet.ToByteArray(); // 자동 직렬화, {data.Length}바이트");
            
            // 역직렬화
            var receivedPacket = PlayerInfoReq.Parser.ParseFrom(data);
            Console.WriteLine($"var receivedPacket = PlayerInfoReq.Parser.ParseFrom(data);");
            Console.WriteLine($"수신된 패킷: PlayerId={receivedPacket.PlayerId}, Name={receivedPacket.Name}");
            Console.WriteLine($"스킬 개수: {receivedPacket.Skills.Count}개");
            
            foreach (var skill in receivedPacket.Skills)
            {
                Console.WriteLine($"  스킬 ID:{skill.Id}, 레벨:{skill.Level}, 지속시간:{skill.Duration}초");
            }
            
            Console.WriteLine();
            Console.WriteLine("🎉 복잡한 중첩 구조와 배열도 한 번에 처리됨!");
            Console.WriteLine("   바이트 위치 계산, 배열 길이 처리 등 모두 자동!");
            Console.WriteLine();
        }

        // 실제 네트워크에서 사용할 때의 예제
        public static byte[] SerializePacket<T>(T packet) where T : IMessage<T>
        {
            return packet.ToByteArray();
        }

        public static T DeserializePacket<T>(byte[] data, MessageParser<T> parser) where T : IMessage<T>
        {
            return parser.ParseFrom(data);
        }

        static void PacketIDExample()
        {
            Console.WriteLine("--- PacketID Enum 사용 예제 ---");
            
            // 기존 수동 enum과 비교
            Console.WriteLine("기존 수동 방식:");
            Console.WriteLine("public enum PacketID");
            Console.WriteLine("{");
            Console.WriteLine("    PlayerInfoReq = 1,");
            Console.WriteLine("    PlayerInfoOk = 2,");
            Console.WriteLine("}");
            Console.WriteLine("// 클라이언트/서버 각각 관리해야 함, 동기화 문제 발생");
            Console.WriteLine();
            
            Console.WriteLine("Protocol Buffers 방식 (.proto에서 자동 생성):");
            Console.WriteLine("enum PacketID {");
            Console.WriteLine("    PACKET_ID_UNKNOWN = 0;");
            Console.WriteLine("    PACKET_ID_PLAYER_INFO_REQ = 1;");
            Console.WriteLine("    PACKET_ID_PLAYER_INFO_RES = 2;");
            Console.WriteLine("    PACKET_ID_CHAT_MESSAGE = 3;");
            Console.WriteLine("    PACKET_ID_HELLO_PACKET = 4;");
            Console.WriteLine("}");
            Console.WriteLine();
            
            Console.WriteLine("C#에서 사용법:");
            
            // PacketID enum 사용 예제
            PacketID chatId = PacketID.ChatMessage;
            PacketID helloId = PacketID.HelloPacket;
            PacketID playerReqId = PacketID.PlayerInfoReq;
            
            Console.WriteLine($"PacketID chatId = PacketID.ChatMessage;        // {chatId} = {(int)chatId}");
            Console.WriteLine($"PacketID helloId = PacketID.HelloPacket;       // {helloId} = {(int)helloId}");
            Console.WriteLine($"PacketID playerReqId = PacketID.PlayerInfoReq; // {playerReqId} = {(int)playerReqId}");
            Console.WriteLine();
            
            // 실제 사용 시나리오
            Console.WriteLine("실제 네트워크에서 사용:");
            
            var chatPacket = new ChatMessage();
            chatPacket.Sender = "Player1";
            chatPacket.Message = "Hello World";
            chatPacket.Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            
            // 패킷 헤더에 ID 포함하는 방식 예제
            Console.WriteLine("// 패킷 전송 시");
            Console.WriteLine($"PacketID packetId = PacketID.ChatMessage;  // {(int)PacketID.ChatMessage}");
            Console.WriteLine("byte[] packetData = chatPacket.ToByteArray();");
            Console.WriteLine("// 실제로는 packetId와 packetData를 함께 전송");
            Console.WriteLine();
            
            // switch문 사용 예제
            Console.WriteLine("패킷 수신 처리 예제:");
            Console.WriteLine("switch (receivedPacketId)");
            Console.WriteLine("{");
            Console.WriteLine($"    case PacketID.ChatMessage:         // {(int)PacketID.ChatMessage}");
            Console.WriteLine("        var chat = ChatMessage.Parser.ParseFrom(data);");
            Console.WriteLine("        HandleChatMessage(chat);");
            Console.WriteLine("        break;");
            Console.WriteLine($"    case PacketID.HelloPacket:         // {(int)PacketID.HelloPacket}");
            Console.WriteLine("        var hello = HelloPacket.Parser.ParseFrom(data);");
            Console.WriteLine("        HandleHelloPacket(hello);");
            Console.WriteLine("        break;");
            Console.WriteLine($"    case PacketID.PlayerInfoReq:       // {(int)PacketID.PlayerInfoReq}");
            Console.WriteLine("        var playerReq = PlayerInfoReq.Parser.ParseFrom(data);");
            Console.WriteLine("        HandlePlayerInfoReq(playerReq);");
            Console.WriteLine("        break;");
            Console.WriteLine("}");
            Console.WriteLine();
            
            Console.WriteLine("✅ 장점:");
            Console.WriteLine("   - 클라이언트/서버 자동 동기화");
            Console.WriteLine("   - 컴파일 타임 타입 체크");
            Console.WriteLine("   - IntelliSense 지원");
            Console.WriteLine("   - 실수로 잘못된 ID 사용 방지");
            Console.WriteLine();
        }

        static void NetworkExample()
        {
            Console.WriteLine("--- 네트워크 전송 시뮬레이션 ---");
            
            // 클라이언트에서 서버로 채팅 전송
            var clientChat = new ChatMessage();
            clientChat.Sender = "Client001";
            clientChat.Message = "서버야 안녕!";
            clientChat.Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            
            byte[] chatData = SerializePacket(clientChat);
            Console.WriteLine($"클라이언트 -> 서버: 채팅 메시지 {chatData.Length}바이트 전송");
            
            // 서버에서 받기
            var serverChat = DeserializePacket(chatData, ChatMessage.Parser);
            Console.WriteLine($"서버가 받은 채팅: [{serverChat.Sender}] {serverChat.Message}");
            
            // 서버에서 다른 클라이언트들에게 브로드캐스트
            var broadcastChat = new ChatMessage();
            broadcastChat.Sender = serverChat.Sender;
            broadcastChat.Message = serverChat.Message;
            broadcastChat.Timestamp = serverChat.Timestamp;
            
            byte[] broadcastData = SerializePacket(broadcastChat);
            Console.WriteLine($"서버 -> 모든 클라이언트: 브로드캐스트 {broadcastData.Length}바이트");
            
            // 다른 클라이언트가 받기
            var otherClientChat = DeserializePacket(broadcastData, ChatMessage.Parser);
            Console.WriteLine($"다른 클라이언트가 받은 채팅: [{otherClientChat.Sender}] {otherClientChat.Message}");
            Console.WriteLine();
        }

        static void ShowComplexPacketExample()
        {
            Console.WriteLine("사용자가 보여준 복잡한 패킷 예제:");
            Console.WriteLine("PlayerInfoReq packet = new PlayerInfoReq()");
            Console.WriteLine("{ playerId = 1001, name = \"ABCD\" };");
            Console.WriteLine("packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 101, level = 1, duration = 1.0f });");
            Console.WriteLine("packet.skills.Add(new PlayerInfoReq.SkillInfo() { id = 201, level = 2, duration = 2.0f });");
            Console.WriteLine("// ... 더 많은 스킬들");
            Console.WriteLine("ArraySegment<byte> s = packet.Write(); // 수동 직렬화");
            Console.WriteLine();
            
            Console.WriteLine("이런 복잡한 구조를 Protocol Buffers로 만들려면:");
            Console.WriteLine("=== playerinfo.proto 파일 ===");
            Console.WriteLine("syntax = \"proto3\";");
            Console.WriteLine("option csharp_namespace = \"GamePackets\";");
            Console.WriteLine();
            Console.WriteLine("message PlayerInfoReq {");
            Console.WriteLine("    int64 player_id = 1;");
            Console.WriteLine("    string name = 2;");
            Console.WriteLine("    repeated SkillInfo skills = 3;  // repeated = 배열/리스트");
            Console.WriteLine("    ");
            Console.WriteLine("    // 중첩 메시지 (nested message)");
            Console.WriteLine("    message SkillInfo {");
            Console.WriteLine("        int32 id = 1;");
            Console.WriteLine("        int32 level = 2;");
            Console.WriteLine("        float duration = 3;");
            Console.WriteLine("    }");
            Console.WriteLine("}");
            Console.WriteLine();
            
            Console.WriteLine("그러면 사용법이 다음과 같이 됩니다:");
            Console.WriteLine("var packet = new PlayerInfoReq();");
            Console.WriteLine("packet.PlayerId = 1001;");
            Console.WriteLine("packet.Name = \"ABCD\";");
            Console.WriteLine();
            Console.WriteLine("// 스킬 추가 (기존과 거의 동일!)");
            Console.WriteLine("packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 101, Level = 1, Duration = 1.0f });");
            Console.WriteLine("packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 201, Level = 2, Duration = 2.0f });");
            Console.WriteLine("packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 301, Level = 3, Duration = 3.0f });");
            Console.WriteLine("packet.Skills.Add(new PlayerInfoReq.Types.SkillInfo { Id = 401, Level = 4, Duration = 4.0f });");
            Console.WriteLine();
            Console.WriteLine("// 직렬화 (복잡한 리스트와 중첩 구조도 한 번에!)");
            Console.WriteLine("byte[] data = packet.ToByteArray(); // 끝!");
            Console.WriteLine();
            Console.WriteLine("// 역직렬화");
            Console.WriteLine("var receivedPacket = PlayerInfoReq.Parser.ParseFrom(data);");
            Console.WriteLine("Console.WriteLine($\"PlayerId: {receivedPacket.PlayerId}, 스킬 개수: {receivedPacket.Skills.Count}\");");
            Console.WriteLine();
            
            Console.WriteLine("핵심 차이점:");
            Console.WriteLine("✅ 바이트 위치 계산 필요 없음");
            Console.WriteLine("✅ 배열 길이 계산 자동 처리");
            Console.WriteLine("✅ 중첩 클래스 자동 생성");
            Console.WriteLine("✅ 타입 안전성 보장");
            Console.WriteLine("✅ 하위 호환성 자동 지원");
            Console.WriteLine();
        }
    }
}