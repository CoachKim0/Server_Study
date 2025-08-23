# MMO Server Study Project

## 프로젝트 개요

C# 기반의 고성능 MMO 게임 서버 학습 프로젝트입니다. TCP 소켓 프로그래밍, 비동기 I/O, 멀티스레딩 등의 핵심 서버 개발 기술을 학습할 수 있도록 구성되었습니다.

## 프로젝트 구조

```
Server_Study/
├── Core/                    # 네트워크 코어 라이브러리
│   ├── Session.cs          # 추상 세션 클래스
│   ├── Listener.cs         # 서버 측 연결 수락
│   ├── Connector.cs        # 클라이언트 측 연결
│   ├── RecvBuffer.cs       # 수신 버퍼 관리
│   └── SendBuffer.cs       # 송신 버퍼 관리
├── Server/                  # 게임 서버 구현
│   └── Program.cs          # 메인 서버 애플리케이션
├── ARCHITECTURE.md          # 아키텍처 문서
└── README.md               # 이 파일
```

## 핵심 기능 상세 설명

### 1. Session 클래스 (Core/Session.cs)

**목적**: 클라이언트와의 네트워크 통신을 담당하는 추상 클래스

**주요 기능**:
- **비동기 데이터 송수신**: `SocketAsyncEventArgs`를 활용한 고성능 I/O
- **버퍼 관리**: 수신/송신 데이터의 효율적인 버퍼링
- **연결 상태 관리**: 스레드 안전한 연결/해제 처리
- **템플릿 메서드 패턴**: 하위 클래스에서 게임 로직 구현

**핵심 설계 원칙**:
```csharp
// 1. 스레드 안전성
private int _disconnect = 0;  // Interlocked로 원자적 연산
object _lock = new object();  // 송신 큐 동기화

// 2. 비동기 I/O
SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();
SocketAsyncEventArgs recvArgs = new SocketAsyncEventArgs();

// 3. 버퍼링
private Queue<ArraySegment<byte>> sendQueue;  // 송신 대기열
private RecvBuffer recvBuffer;                // 수신 버퍼
```

**왜 이렇게 설계했을까?**
- **성능**: 동기식 I/O는 스레드를 블록시키지만, 비동기 I/O는 OS의 완료 포트를 활용
- **확장성**: 수천 개의 동시 연결을 적은 스레드로 처리 가능
- **안정성**: 스레드 안전한 연결 관리로 경쟁 상태(Race Condition) 방지

### 2. Listener 클래스 (Core/Listener.cs)

**목적**: 서버에서 클라이언트의 연결 요청을 수락하는 역할

**동작 과정**:
1. **초기화**: `Init()` 메서드로 엔드포인트 바인딩
2. **수락 대기**: `AcceptAsync()`로 비동기 연결 대기
3. **세션 생성**: 연결 성공 시 팩토리 함수로 새 세션 생성
4. **재귀 대기**: 다음 연결을 위해 다시 Accept 등록

```csharp
// 핵심 로직 흐름
listener.Init(endPoint, () => new GameSession());
// ↓
AcceptAsync() // 비동기 대기
// ↓
OnAcceptComplete() // 연결 완료 시 호출
// ↓  
sessionFactory.Invoke() // 새 세션 생성
// ↓
RegisterAccept() // 다음 연결 대기
```

**장점**:
- **높은 처리량**: 한 번에 하나씩이 아닌 동시 다중 연결 처리
- **리소스 효율성**: 스레드 풀 활용으로 메모리 사용량 최적화

### 3. RecvBuffer 클래스 (Core/RecvBuffer.cs)

**목적**: 수신된 데이터의 효율적인 버퍼링 및 파싱

**핵심 개념**:
```
Buffer: [읽은데이터][처리할데이터][빈공간]
         ^          ^            ^
       readPos    writePos     buffer.end
```

**주요 메서드**:
- `WriteSegment`: 새 데이터를 받을 수 있는 영역 반환
- `ReadSegment`: 처리할 데이터가 있는 영역 반환  
- `Clean()`: 처리된 데이터 정리 및 버퍼 최적화
- `OnWrite()`: 데이터 수신 후 writePos 이동
- `OnRead()`: 데이터 처리 후 readPos 이동

**왜 이런 구조인가?**
- **메모리 효율성**: 매번 새 배열을 만들지 않고 기존 버퍼 재사용
- **파싱 안정성**: 불완전한 패킷도 안전하게 처리
- **성능**: 불필요한 메모리 복사 최소화

### 4. SendBuffer 클래스 (Core/SendBuffer.cs)

**목적**: 송신 데이터의 효율적인 버퍼 관리

**TLS(Thread Local Storage) 활용**:
```csharp
public static ThreadLocal<SendBuffer> CurrentBuffer = 
    new ThreadLocal<SendBuffer>(() => null);
```

**사용 패턴**:
```csharp
// 1. 버퍼 예약
ArraySegment<byte> segment = SendBufferHelper.Open(1024);

// 2. 데이터 작성
BitConverter.GetBytes(data).CopyTo(segment.Array, segment.Offset);

// 3. 실제 사용 크기 확정
ArraySegment<byte> finalBuffer = SendBufferHelper.Close(actualSize);

// 4. 전송
session.Send(finalBuffer);
```

**설계 이유**:
- **스레드 안전성**: 각 스레드마다 독립적인 버퍼 사용
- **메모리 풀링**: 버퍼 재사용으로 GC 압박 감소
- **성능 최적화**: 메모리 할당/해제 비용 최소화

### 5. Connector 클래스 (Core/Connector.cs)

**목적**: 클라이언트에서 서버로의 연결 담당 (분산 서버용)

**사용 시나리오**:
- 게임 서버 ↔ 로그인 서버
- 게임 서버 ↔ 데이터베이스 서버
- 서버 간 내부 통신

**동작 방식**:
```csharp
connector.Connect(serverEndPoint, () => new ClientSession());
```

### 6. GameSession 클래스 (Server/Program.cs)

**목적**: 실제 게임 로직을 처리하는 구체적인 세션 구현

**구현된 기능**:
- **OnConnected**: 클라이언트 연결 시 초기 게임 데이터 전송
- **OnRecv**: 클라이언트로부터 받은 데이터 처리
- **OnSend**: 데이터 전송 완료 알림
- **OnDisconnected**: 연결 종료 시 정리 작업

**데이터 직렬화 예시**:
```csharp
// Knight 구조체를 바이너리로 변환
byte[] hpBytes = BitConverter.GetBytes(knight.hp);
byte[] attackBytes = BitConverter.GetBytes(knight.attack);
// 버퍼에 순서대로 복사하여 전송
```

## 성능 최적화 포인트

### 1. 비동기 I/O (Asynchronous I/O)
**문제**: 동기식 I/O는 스레드를 블록시켜 리소스 낭비
**해결**: `SocketAsyncEventArgs` 사용으로 논블록킹 I/O 구현
**결과**: 적은 스레드로 대량의 동시 연결 처리 가능

### 2. 버퍼 풀링 (Buffer Pooling)
**문제**: 매번 새로운 배열 생성 시 GC 압박
**해결**: TLS 기반 버퍼 재사용
**결과**: 메모리 할당 횟수 감소 → 성능 향상

### 3. 배치 전송 (Batch Sending)
**문제**: 작은 데이터 여러 번 전송 시 네트워크 효율성 저하
**해결**: 송신 큐에 모아서 한 번에 전송
**결과**: 네트워크 오버헤드 감소

### 4. 스레드 안전성 (Thread Safety)
**문제**: 멀티스레드 환경에서 데이터 경쟁
**해결**: `lock`, `Interlocked` 연산 사용
**결과**: 안정적인 동시성 처리

## 학습 포인트

### 초급
1. **TCP 소켓 기초**: `Socket` 클래스 사용법
2. **이벤트 기반 프로그래밍**: 콜백 패턴 이해
3. **버퍼 관리**: 바이트 배열과 `ArraySegment` 활용

### 중급
1. **비동기 프로그래밍**: `SocketAsyncEventArgs` 패턴
2. **메모리 관리**: 객체 풀링과 GC 최적화
3. **스레드 동기화**: `lock`과 `Interlocked` 사용법

### 고급
1. **고성능 서버 아키텍처**: IOCP 기반 설계
2. **확장 가능한 설계**: 팩토리 패턴과 추상화
3. **성능 튜닝**: 병목 지점 분석 및 최적화

## 실행 방법

1. **서버 실행**:
   ```bash
   cd Server
   dotnet run
   ```

2. **클라이언트 테스트**: 
   - Telnet이나 별도 클라이언트로 localhost:7777 연결
   - 서버에서 Knight 데이터를 받은 후 자동 연결 종료

## 확장 아이디어

1. **패킷 시스템**: 구조화된 메시지 프로토콜 구현
2. **룸 시스템**: 여러 플레이어가 참여하는 게임 룸
3. **데이터베이스 연동**: Entity Framework로 플레이어 정보 저장
4. **로드 밸런싱**: 여러 서버 인스턴스 간 부하 분산
5. **모니터링**: 성능 메트릭 수집 및 로깅

이 프로젝트를 통해 MMO 서버의 핵심 개념들을 단계별로 학습하고, 실제 상용 서버 개발에 필요한 기초를 다질 수 있습니다.