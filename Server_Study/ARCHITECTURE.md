# MMO Server Architecture

## 전체 아키텍처 다이어그램

```mermaid
graph TB
    subgraph "Client Side"
        C[Client Applications]
    end
    
    subgraph "Network Layer"
        N[Network (TCP/IP)]
    end
    
    subgraph "Server Application"
        subgraph "Server Project"
            P[Program.cs<br/>서버 진입점]
            GS[GameSession<br/>게임 로직]
            K[Knight<br/>게임 데이터]
        end
        
        subgraph "ServerCore Library"
            L[Listener<br/>연결 수락]
            S[Session<br/>통신 관리]
            CO[Connector<br/>클라이언트 연결]
            RB[RecvBuffer<br/>수신 버퍼]
            SB[SendBuffer<br/>송신 버퍼]
        end
    end
    
    C ---|TCP Connection| N
    N ---|Accept/Connect| L
    L ---|Create| GS
    GS ---|Inherit| S
    S ---|Use| RB
    S ---|Use| SB
    CO ---|Create| S
    P ---|Initialize| L
    P ---|Create Factory| GS
    GS ---|Use| K
```

## 클래스 관계도

```mermaid
classDiagram
    class Session {
        <<abstract>>
        -Socket socket
        -RecvBuffer recvBuffer
        -Queue~ArraySegment~byte~~ sendQueue
        -int _disconnect
        +OnConnected(EndPoint)* void
        +OnRecv(ArraySegment~byte~)* int
        +OnSend(int)* void
        +OnDisconnected(EndPoint)* void
        +Start(Socket) void
        +Send(ArraySegment~byte~) void
        +Disconnect() void
    }
    
    class GameSession {
        +OnConnected(EndPoint) void
        +OnRecv(ArraySegment~byte~) int
        +OnSend(int) void
        +OnDisconnected(EndPoint) void
    }
    
    class Listener {
        -Socket _listenSocket
        -Func~Session~ sessionFactory
        +Init(IPEndPoint, Func~Session~) void
        -RegisterAccept(SocketAsyncEventArgs) void
        -OnAcceptComplete(object, SocketAsyncEventArgs) void
    }
    
    class Connector {
        -Func~Session~ sessionFactory
        +Connect(IPEndPoint, Func~Session~) void
        -RegisterConnect(SocketAsyncEventArgs) void
        -OnConnectComplete(object, SocketAsyncEventArgs) void
    }
    
    class RecvBuffer {
        -ArraySegment~byte~ buffer
        -int readPos
        -int writePos
        +DataSize int
        +FreeSize int
        +ReadSegment ArraySegment~byte~
        +WriteSegment ArraySegment~byte~
        +Clean() void
        +OnRead(int) bool
        +OnWrite(int) bool
    }
    
    class SendBuffer {
        -byte[] buffer
        -int useSize
        +FreeSize int
        +Open(int) ArraySegment~byte~
        +Close(int) ArraySegment~byte~
    }
    
    class SendBufferHelper {
        +CurrentBuffer ThreadLocal~SendBuffer~
        +ChunkSize int
        +Open(int)$ ArraySegment~byte~
        +Close(int)$ ArraySegment~byte~
    }
    
    class Knight {
        +int hp
        +int attack
    }
    
    class Program {
        -Listener listener$
        +Main(string[])$ void
    }
    
    Session <|-- GameSession
    Session --> RecvBuffer : uses
    Session --> SendBuffer : uses
    Listener --> Session : creates
    Connector --> Session : creates
    GameSession --> Knight : uses
    Program --> Listener : uses
    Program --> GameSession : factory
    SendBufferHelper --> SendBuffer : manages
```

## 데이터 흐름도

### 1. 서버 시작 및 클라이언트 연결 흐름

```mermaid
sequenceDiagram
    participant P as Program
    participant L as Listener
    participant C as Client
    participant GS as GameSession
    participant S as Session
    
    P->>L: Init(endPoint, sessionFactory)
    L->>L: Bind & Listen on port 7777
    Note over L: 클라이언트 연결 대기 시작
    
    C->>L: TCP Connect Request
    L->>L: AcceptAsync()
    L->>GS: sessionFactory() - Create new GameSession
    L->>GS: Start(acceptedSocket)
    GS->>S: 부모 클래스 초기화
    S->>S: RegisterRecv() - 수신 대기 시작
    L->>GS: OnConnected(clientEndPoint)
    
    Note over GS: 클라이언트에게 초기 게임 데이터 전송
    GS->>GS: Create Knight data
    GS->>S: Send(knightData)
    S->>C: 게임 데이터 전송
    GS->>S: Disconnect() (테스트용)
```

### 2. 데이터 송수신 흐름

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Session
    participant RB as RecvBuffer
    participant SB as SendBuffer
    participant GS as GameSession
    
    Note over C,GS: 데이터 수신 흐름
    C->>S: Send TCP Data
    S->>S: OnRecvCompleted()
    S->>RB: OnWrite(receivedBytes)
    S->>GS: OnRecv(readSegment)
    GS->>GS: Process game logic
    GS-->>S: return processedBytes
    S->>RB: OnRead(processedBytes)
    
    Note over C,GS: 데이터 송신 흐름
    GS->>SB: SendBufferHelper.Open(size)
    GS->>SB: Copy data to buffer
    GS->>SB: SendBufferHelper.Close(actualSize)
    GS->>S: Send(bufferSegment)
    S->>S: Enqueue to sendQueue
    S->>S: RegisterSend()
    S->>C: SendAsync()
    S->>S: OnSendCompleted()
    S->>GS: OnSend(sentBytes)
```

## 네트워크 통신 시퀀스

### 비동기 I/O 처리 과정

```mermaid
graph LR
    subgraph "Main Thread"
        MT[Main Thread<br/>무한 대기]
    end
    
    subgraph "Thread Pool"
        TP1[Accept Handler]
        TP2[Recv Handler]
        TP3[Send Handler]
    end
    
    subgraph "OS Kernel"
        K[Kernel I/O<br/>Completion Port]
    end
    
    MT -.-> TP1
    MT -.-> TP2
    MT -.-> TP3
    
    TP1 <--> K
    TP2 <--> K
    TP3 <--> K
```

## 주요 설계 패턴

### 1. Factory Pattern
- `Listener`와 `Connector`에서 `Func<Session>` 팩토리 사용
- 다양한 타입의 세션을 동적으로 생성 가능

### 2. Template Method Pattern
- `Session` 추상 클래스의 `OnConnected`, `OnRecv`, `OnSend`, `OnDisconnected`
- 하위 클래스에서 게임 로직에 맞게 구현

### 3. Object Pool Pattern
- `SendBufferHelper`의 TLS 기반 버퍼 재사용
- 메모리 할당/해제 비용 최소화

### 4. Observer Pattern
- `SocketAsyncEventArgs`의 `Completed` 이벤트 처리
- 비동기 I/O 완료 시점을 콜백으로 처리