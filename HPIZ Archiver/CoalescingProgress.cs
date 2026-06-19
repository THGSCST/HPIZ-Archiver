using System;
using System.Threading;

namespace HPIZArchiver
{
    internal sealed class CoalescingProgress<T> : IProgress<T>
    {
        private readonly SynchronizationContext synchronizationContext;
        private readonly Action<T, int> handler;
        private readonly object valueLock = new object();

        private T latestValue;
        private int pendingCount;
        private int callbackScheduled;

        public CoalescingProgress(Action<T, int> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            synchronizationContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("A synchronization context is required.");
            this.handler = handler;
        }

        public void Report(T value)
        {
            lock (valueLock)
                latestValue = value;

            Interlocked.Increment(ref pendingCount);
            ScheduleCallback();
        }

        private void ScheduleCallback()
        {
            if (Interlocked.CompareExchange(ref callbackScheduled, 1, 0) == 0)
                synchronizationContext.Post(_ => Drain(), null);
        }

        private void Drain()
        {
            int count = Interlocked.Exchange(ref pendingCount, 0);
            T value;
            lock (valueLock)
                value = latestValue;

            if (count > 0)
                handler(value, count);

            Interlocked.Exchange(ref callbackScheduled, 0);
            if (Volatile.Read(ref pendingCount) > 0)
                ScheduleCallback();
        }
    }
}
