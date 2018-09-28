using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReduxPattern
{
    public class RetryPolicy
    {
        private readonly Func<bool> shouldRetry;
        private readonly Action incrementRetryCount;
        private readonly Func<Task> delay;

        public RetryPolicy(Func<bool> shouldRetry, Action incrementRetryCount, Func<Task> delay)
        {
            this.shouldRetry = shouldRetry;
            this.incrementRetryCount = incrementRetryCount;
            this.delay = delay;
        }

        public bool ShouldRetry => shouldRetry();
        public void IncrementRetryCount() => incrementRetryCount();
        public Task Delay => delay();

        public static RetryPolicy Immediate(int maxRetryCount)
        {
            int retryCount = 0;
            return new RetryPolicy(
                shouldRetry: () => retryCount < maxRetryCount,
                incrementRetryCount: () => retryCount++,
                delay: () => Task.FromResult(true)
            );
        }

        public static RetryPolicy Constant(int millisecondsBackoff, int maxRetryCount)
        {
            int retryCount = 0;
            return new RetryPolicy(
                shouldRetry: () => retryCount < maxRetryCount,
                incrementRetryCount: () => retryCount++,
                delay: () => Task.Delay(millisecondsBackoff)
            );
        }

        public static RetryPolicy Random(int millisecondsBackoffMin, int millisecondsBackoffMax, int maxRetryCount)
        {
            var random = new Random();

            int retryCount = 0;
            return new RetryPolicy(
                shouldRetry: () => retryCount < maxRetryCount,
                incrementRetryCount: () => retryCount++,
                delay: () => Task.Delay(random.Next(millisecondsBackoffMin, millisecondsBackoffMax))
            );
        }
    }

    public class RetryContext<T>
    {
        internal Func<Task<T>> Execute { get; set; }
        internal Task<T> Value { get; set; }
        internal bool ContinueOnCapturedContext { get; set; }
    }
}

namespace ReduxPattern.TaskExtension
{
    public static class Task
    {
        public static System.Threading.Tasks.Task Create<T>(Func<CancellationToken, Task<T>> createProc, TimeSpan timeout)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            return createProc(cts.Token);
        }

        public static RetryContext<T> Retry<T>(Func<Task<T>> retryProc, bool continueOnCapturedContext = false) =>
            new RetryContext<T>
            {
                Execute = retryProc,
                Value = retryProc(),
                ContinueOnCapturedContext = continueOnCapturedContext
            };

        public static RetryContext<T> When<T, TException>(this RetryContext<T> self, RetryPolicy retryPolicy)
            where TException : Exception
        {
            self.Value = self.Value.Retry<TException, T>(retryPolicy, self.Execute, self.ContinueOnCapturedContext);
            return self;
        }

        public static Task<T> Value<T>(this RetryContext<T> self) => self.Value;

        private static async Task<T> Retry<TException, T>(this Task<T> self, RetryPolicy retryPolicy, Func<Task<T>> retryProc, bool continueOnCapturedContext)
            where TException : Exception
        {
            TException ex;
            do
            {
                try
                {
                    return await self.ConfigureAwait(continueOnCapturedContext);
                }
                catch (TException e)
                {
                    ex = e;

                    await retryPolicy.Delay.ConfigureAwait(continueOnCapturedContext);

                    self = retryProc();
                    retryPolicy.IncrementRetryCount();
                }
            }
            while (retryPolicy.ShouldRetry);

            throw ex;
        }
    }
}
