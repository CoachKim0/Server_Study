using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCore
{
    public interface IJob
    {
        void Execute();
    }

    public class Job : IJob
    {
        private Action _action;

        public Job(Action action)
        {
            _action = action;
        }

        public void Execute()
        {
            _action?.Invoke();
        }
    }

    public class JobQueue
    {
        private readonly ConcurrentQueue<IJob> _jobQueue = new ConcurrentQueue<IJob>();
        private readonly object _lock = new object();
        private bool _isRunning = false;

        public void Push(IJob job)
        {
            _jobQueue.Enqueue(job);

            lock (_lock)
            {
                if (!_isRunning)
                {
                    _isRunning = true;
                    Task.Run(ProcessJobs);
                }
            }
        }

        public void Push(Action action)
        {
            Push(new Job(action));
        }

        private void ProcessJobs()
        {
            while (true)
            {
                if (_jobQueue.TryDequeue(out IJob? job))
                {
                    try
                    {
                        job.Execute();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[JobQueue Error] {ex.Message}");
                    }
                }
                else
                {
                    lock (_lock)
                    {
                        if (_jobQueue.IsEmpty)
                        {
                            _isRunning = false;
                            break;
                        }
                    }
                }
            }
        }

        public int Count => _jobQueue.Count;
        public bool IsEmpty => _jobQueue.IsEmpty;
    }

    // 전역 JobQueue 매니저 (싱글톤)
    public class JobQueueManager
    {
        private static JobQueueManager? _instance = null;
        private static readonly object _instanceLock = new object();
        
        private readonly Dictionary<string, JobQueue> _jobQueues = new Dictionary<string, JobQueue>();
        private readonly object _queueLock = new object();

        public static JobQueueManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                            _instance = new JobQueueManager();
                    }
                }
                return _instance;
            }
        }

        public JobQueue GetOrCreateQueue(string queueName)
        {
            lock (_queueLock)
            {
                if (!_jobQueues.ContainsKey(queueName))
                {
                    _jobQueues[queueName] = new JobQueue();
                }
                return _jobQueues[queueName];
            }
        }

        public void PushJob(string queueName, IJob job)
        {
            GetOrCreateQueue(queueName).Push(job);
        }

        public void PushJob(string queueName, Action action)
        {
            GetOrCreateQueue(queueName).Push(action);
        }

        // 통계 정보
        public Dictionary<string, int> GetQueueStats()
        {
            lock (_queueLock)
            {
                var stats = new Dictionary<string, int>();
                foreach (var kvp in _jobQueues)
                {
                    stats[kvp.Key] = kvp.Value.Count;
                }
                return stats;
            }
        }
    }
}