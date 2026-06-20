using System;
using System.Threading;
using System.Threading.Tasks;

namespace HPIZ
{
    internal static class OperationTuning
    {
        private const string WorkerCountEnvironmentVariable = "HPIZ_MAX_WORKERS";

        internal static readonly int WorkerCount = GetWorkerCount();

        internal static readonly ParallelOptions ParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = WorkerCount
        };

        internal static ParallelOptions CreateParallelOptions(CancellationToken cancellationToken)
        {
            return new ParallelOptions
            {
                MaxDegreeOfParallelism = WorkerCount,
                CancellationToken = cancellationToken
            };
        }

        internal static void Initialize()
        {
            int minimumWorkerThreads;
            int minimumCompletionPortThreads;
            ThreadPool.GetMinThreads(out minimumWorkerThreads, out minimumCompletionPortThreads);

            if (minimumWorkerThreads < WorkerCount)
                ThreadPool.SetMinThreads(WorkerCount, minimumCompletionPortThreads);
        }

        private static int GetWorkerCount()
        {
            int configuredWorkerCount;
            string configuredValue = Environment.GetEnvironmentVariable(WorkerCountEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredValue)
                && int.TryParse(configuredValue, out configuredWorkerCount)
                && configuredWorkerCount > 0)
                return configuredWorkerCount;

            return Math.Max(1, Environment.ProcessorCount);
        }
    }
}
