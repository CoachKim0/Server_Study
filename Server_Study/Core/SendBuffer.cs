using System.Net.Http.Headers;

namespace ServerCore
{
    /// <summary>
    /// 송신 버퍼를 TLS(Thread Local Storage) 기반으로 관리하는 헬퍼 클래스
    /// 각 스레드마다 독립적인 버퍼를 사용하여 스레드 안전성을 보장
    /// 네트워크 송신 성능을 최적화하기 위한 메모리 풀링 기능 제공
    /// </summary>
    public class SendBufferHelper
    {
        /// <summary>
        /// 각 스레드별 전용 버퍼를 반환하는 TLS 변수
        /// </summary>
        public static ThreadLocal<SendBuffer?> CurrentBuffer = new ThreadLocal<SendBuffer?>(() => null);
        
        /// <summary>
        /// 버퍼 청크 크기 (기본값: 8KB로 증가)
        /// </summary>
        public static int ChunkSize { get; set; } = 8192;

        /// <summary>
        /// 송신할 데이터 영역을 예약하고 버퍼 세그먼트를 반환
        /// </summary>
        /// <param name="reserveSize">예약할 바이트 크기</param>
        /// <returns>예약된 버퍼 영역의 ArraySegment</returns>
        public static ArraySegment<byte> Open(int reserveSize)
        {
            // 현재 스레드에 버퍼가 없으면 새로 생성
            if ( CurrentBuffer.Value == null )
                CurrentBuffer.Value = new SendBuffer(ChunkSize);

            // 남은 공간이 부족하면 새 버퍼 생성
            if (CurrentBuffer.Value.FreeSize < reserveSize)
                CurrentBuffer.Value = new SendBuffer(ChunkSize);
            
            return CurrentBuffer.Value.Open(reserveSize);
        }

        /// <summary>
        /// 예약된 영역의 실제 사용량을 확정하고 최종 버퍼 세그먼트를 반환
        /// </summary>
        /// <param name="useSize">실제 사용한 바이트 크기</param>
        /// <returns>실제 데이터가 담긴 버퍼 세그먼트</returns>
        public static ArraySegment<byte> Close(int useSize)
        {
            return CurrentBuffer.Value!.Close(useSize);
        }
    }


    /// <summary>
    /// 송신용 데이터를 임시 저장하는 버퍼 클래스
    /// 메모리 할당/해제 비용을 줄이고 성능을 향상시키기 위한 버퍼 풀링
    /// </summary>
    public class SendBuffer
    {
        /// <summary>
        /// 실제 데이터를 저장하는 바이트 배열
        /// [사용된 영역][빈 영역]으로 구성
        /// </summary>
        byte[] buffer;
        
        /// <summary>
        /// 현재까지 사용된 버퍼 크기
        /// </summary>
        private int useSize = 0;

        /// <summary>
        /// 버퍼에 남은 사용 가능한 공간 크기
        /// </summary>
        public int FreeSize
        {
            get => this.buffer.Length - this.useSize;
        }

        /// <summary>
        /// 지정된 크기로 송신 버퍼를 초기화
        /// </summary>
        /// <param name="chunkSize">버퍼의 전체 크기</param>
        public SendBuffer(int chunkSize)
        {
            buffer = new byte[chunkSize];
        }


        /// <summary>
        /// 예상하는 최대 사이즈,,얼마만큼의 사이즈를 최대치를 사용할 건지를 매개변수로 넘겨준다
        /// </summary>
        /// <param name="reserveSize"></param>
        /// <returns></returns>
        public ArraySegment<byte> Open(int reserveSize)
        {
            if (reserveSize > FreeSize)
                return new ArraySegment<byte>();

            return new ArraySegment<byte>(buffer, useSize, reserveSize);
        }

        /// <summary>
        /// 실제 썼다고 확인된 사이즈
        /// </summary>
        /// <param name="useSize"></param>
        /// <returns></returns>
        public ArraySegment<byte> Close(int usesize)
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer, useSize, usesize);
            useSize += usesize;
            return segment;
        }
    }
}