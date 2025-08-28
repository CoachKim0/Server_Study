using ServerCore;
using System.Diagnostics;

namespace Server_Study
{
    public class JobQueuePerformanceTest
    {
        public static void RunBenchmark()
        {
            Console.WriteLine("=== JobQueue 성능 테스트 시작 ===");
            
            // Test 1: 단순 작업 처리 성능
            TestSimpleJobs();
            
            // Test 2: 동시 작업 추가 성능
            TestConcurrentJobs();
            
            Console.WriteLine("=== JobQueue 성능 테스트 완료 ===\n");
        }

        private static void TestSimpleJobs()
        {
            Console.WriteLine("[Test 1] 단순 작업 처리 성능 테스트");
            
            var jobQueue = new JobQueue();
            const int jobCount = 10000;
            int processedJobs = 0;
            
            var stopwatch = Stopwatch.StartNew();
            
            // 대량의 작업을 JobQueue에 추가
            for (int i = 0; i < jobCount; i++)
            {
                int jobId = i;
                jobQueue.Push(() => {
                    // 간단한 연산 시뮬레이션
                    int result = jobId * 2 + 1;
                    Interlocked.Increment(ref processedJobs);
                });
            }
            
            // 모든 작업이 완료될 때까지 대기
            while (processedJobs < jobCount)
            {
                Thread.Sleep(1);
            }
            
            stopwatch.Stop();
            
            Console.WriteLine($"  - 처리한 작업 수: {processedJobs:N0}개");
            Console.WriteLine($"  - 총 소요 시간: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  - 초당 처리량: {jobCount * 1000.0 / stopwatch.ElapsedMilliseconds:F0} jobs/sec");
        }

        private static void TestConcurrentJobs()
        {
            Console.WriteLine("[Test 2] 동시 작업 추가 성능 테스트");
            
            var jobQueue = new JobQueue();
            const int threadCount = 10;
            const int jobsPerThread = 1000;
            const int totalJobs = threadCount * jobsPerThread;
            
            int processedJobs = 0;
            var stopwatch = Stopwatch.StartNew();
            
            // 여러 스레드에서 동시에 작업 추가
            var tasks = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks[t] = Task.Run(() => {
                    for (int i = 0; i < jobsPerThread; i++)
                    {
                        int jobId = threadId * jobsPerThread + i;
                        jobQueue.Push(() => {
                            // CPU 집약적 작업 시뮬레이션
                            var result = Math.Sqrt(jobId * 3.14159);
                            Interlocked.Increment(ref processedJobs);
                        });
                    }
                });
            }
            
            // 모든 스레드가 작업 추가를 완료할 때까지 대기
            Task.WaitAll(tasks);
            
            // 모든 작업이 처리될 때까지 대기
            while (processedJobs < totalJobs)
            {
                Thread.Sleep(1);
            }
            
            stopwatch.Stop();
            
            Console.WriteLine($"  - 동시 스레드 수: {threadCount}개");
            Console.WriteLine($"  - 스레드당 작업 수: {jobsPerThread:N0}개");
            Console.WriteLine($"  - 총 처리 작업 수: {processedJobs:N0}개");
            Console.WriteLine($"  - 총 소요 시간: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  - 초당 처리량: {totalJobs * 1000.0 / stopwatch.ElapsedMilliseconds:F0} jobs/sec");
        }
    }
}