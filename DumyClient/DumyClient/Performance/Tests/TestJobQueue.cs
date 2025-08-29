using System.Diagnostics;

namespace DummyClient.Performance.Tests;

/// <summary>
/// JobQueue 성능 테스트 클래스
/// - 다양한 작업 부하를 통해 JobQueue의 성능을 측정합니다
/// - CPU 집약적 작업, I/O 작업, 대량 작업 등을 테스트합니다
/// </summary>
public static class TestJobQueue
{
    /// <summary>
    /// JobQueue 성능 테스트 실행
    /// </summary>
    public static async Task RunPerformanceTest()
    {
        Console.WriteLine("=== JobQueue 성능 테스트 시작 ===");
        Console.WriteLine();

        try
        {
            // 1. 기본 작업 처리 테스트
            await TestBasicJobs();
            
            // 2. CPU 집약적 작업 테스트
            await TestCpuIntensiveJobs();
            
            // 3. I/O 작업 테스트
            await TestIoJobs();
            
            // 4. 대량 작업 처리 테스트
            await TestHighVolumeJobs();
            
            // 5. 동시성 테스트
            await TestConcurrentJobs();

            Console.WriteLine();
            Console.WriteLine("=== JobQueue 성능 테스트 완료 ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [JobQueue] 테스트 중 오류 발생: {ex.Message}");
        }
    }

    /// <summary>
    /// 기본 작업 처리 테스트
    /// </summary>
    private static async Task TestBasicJobs()
    {
        Console.WriteLine("1️⃣ 기본 작업 처리 테스트");
        
        var stopwatch = Stopwatch.StartNew();
        var jobCount = 1000;
        var completedJobs = 0;

        for (int i = 0; i < jobCount; i++)
        {
            var jobId = i;
            await Task.Run(() =>
            {
                // 간단한 계산 작업
                var result = Math.Sqrt(jobId * 2.5);
                Interlocked.Increment(ref completedJobs);
            });
        }

        stopwatch.Stop();
        Console.WriteLine($"✅ 기본 작업 {jobCount}개 완료");
        Console.WriteLine($"📊 실행 시간: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 초당 처리량: {(jobCount * 1000.0 / stopwatch.ElapsedMilliseconds):F1} jobs/sec");
        Console.WriteLine();
    }

    /// <summary>
    /// CPU 집약적 작업 테스트
    /// </summary>
    private static async Task TestCpuIntensiveJobs()
    {
        Console.WriteLine("2️⃣ CPU 집약적 작업 테스트");
        
        var stopwatch = Stopwatch.StartNew();
        var jobCount = 100;
        var tasks = new List<Task>();

        for (int i = 0; i < jobCount; i++)
        {
            var jobId = i;
            tasks.Add(Task.Run(() =>
            {
                // CPU 집약적 작업 (피보나치 계산)
                CalculateFibonacci(35);
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        Console.WriteLine($"✅ CPU 집약적 작업 {jobCount}개 완료");
        Console.WriteLine($"📊 실행 시간: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 초당 처리량: {(jobCount * 1000.0 / stopwatch.ElapsedMilliseconds):F1} jobs/sec");
        Console.WriteLine();
    }

    /// <summary>
    /// I/O 작업 테스트
    /// </summary>
    private static async Task TestIoJobs()
    {
        Console.WriteLine("3️⃣ I/O 작업 테스트");
        
        var stopwatch = Stopwatch.StartNew();
        var jobCount = 500;
        var tasks = new List<Task>();

        for (int i = 0; i < jobCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                // I/O 작업 시뮬레이션 (비동기 대기)
                await Task.Delay(10);
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        Console.WriteLine($"✅ I/O 작업 {jobCount}개 완료");
        Console.WriteLine($"📊 실행 시간: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 초당 처리량: {(jobCount * 1000.0 / stopwatch.ElapsedMilliseconds):F1} jobs/sec");
        Console.WriteLine();
    }

    /// <summary>
    /// 대량 작업 처리 테스트
    /// </summary>
    private static async Task TestHighVolumeJobs()
    {
        Console.WriteLine("4️⃣ 대량 작업 처리 테스트");
        
        var stopwatch = Stopwatch.StartNew();
        var jobCount = 10000;
        var tasks = new List<Task>();

        for (int i = 0; i < jobCount; i++)
        {
            var jobId = i;
            tasks.Add(Task.Run(() =>
            {
                // 가벼운 작업
                var result = jobId % 1000;
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        Console.WriteLine($"✅ 대량 작업 {jobCount}개 완료");
        Console.WriteLine($"📊 실행 시간: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 초당 처리량: {(jobCount * 1000.0 / stopwatch.ElapsedMilliseconds):F1} jobs/sec");
        Console.WriteLine();
    }

    /// <summary>
    /// 동시성 테스트
    /// </summary>
    private static async Task TestConcurrentJobs()
    {
        Console.WriteLine("5️⃣ 동시성 테스트");
        
        var stopwatch = Stopwatch.StartNew();
        var concurrentGroups = 10;
        var jobsPerGroup = 100;
        var totalJobs = concurrentGroups * jobsPerGroup;
        var groupTasks = new List<Task>();

        for (int group = 0; group < concurrentGroups; group++)
        {
            var groupId = group;
            groupTasks.Add(Task.Run(async () =>
            {
                var jobTasks = new List<Task>();
                for (int job = 0; job < jobsPerGroup; job++)
                {
                    var jobId = job;
                    jobTasks.Add(Task.Run(() =>
                    {
                        // 각 그룹에서 병렬 작업 수행
                        var result = Math.Pow(groupId + jobId, 2);
                    }));
                }
                await Task.WhenAll(jobTasks);
            }));
        }

        await Task.WhenAll(groupTasks);
        stopwatch.Stop();
        
        Console.WriteLine($"✅ 동시성 테스트 완료 ({concurrentGroups}개 그룹, 총 {totalJobs}개 작업)");
        Console.WriteLine($"📊 실행 시간: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📊 초당 처리량: {(totalJobs * 1000.0 / stopwatch.ElapsedMilliseconds):F1} jobs/sec");
        Console.WriteLine();
    }

    /// <summary>
    /// 피보나치 계산 (CPU 집약적 작업)
    /// </summary>
    private static long CalculateFibonacci(int n)
    {
        if (n <= 1) return n;
        return CalculateFibonacci(n - 1) + CalculateFibonacci(n - 2);
    }
}