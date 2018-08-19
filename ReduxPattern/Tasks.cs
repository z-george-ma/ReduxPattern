using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReduxPattern
{
    public class RetryContext<T>
    {
        public bool Break = false;
        public bool ContinueOnCapturedContext = false;
        public int ErrorCount = 0;
        public T Value { get; set; }
    }

    public static class Tasks
    {
        public static async Task<TResult> Then<TResult>(this Task self, Func<Task<TResult>> thenProc, bool continueOnCapturedContext = false)
        {
            await self.ConfigureAwait(continueOnCapturedContext);

            return await thenProc().ConfigureAwait(continueOnCapturedContext);
        }

        public static async Task<TResult> Then<T, TResult>(this Task<T> self, Func<T, Task<TResult>> thenProc, bool continueOnCapturedContext = false)
        {
            var result = await self.ConfigureAwait(continueOnCapturedContext);

            return await thenProc(result).ConfigureAwait(continueOnCapturedContext);
        }

        public static async Task<TResult> Then<TResult>(this Task self, Func<TResult> thenProc, bool continueOnCapturedContext = false)
        {
            await self.ConfigureAwait(continueOnCapturedContext);
            return thenProc();
        }

        public static async Task<TResult> Then<T, TResult>(this Task<T> self, Func<T, TResult> thenProc, bool continueOnCapturedContext = false) =>
            thenProc(await self.ConfigureAwait(continueOnCapturedContext));

        public static async Task<T> Catch<TException, T>(this Task<T> self, Func<TException, Task<T>> catchProc, bool continueOnCapturedContext = false)
            where TException : Exception
        {
            try
            {
                return await self.ConfigureAwait(continueOnCapturedContext);
            }
            catch (TException e)
            {
                return await catchProc(e).ConfigureAwait(continueOnCapturedContext);
            }
        }

        public static Task<T> Catch<TException, T>(this Task<T> self, Func<TException, T> catchProc, bool continueOnCapturedContext = false)
            where TException : Exception =>
            self.Catch((TException e) => Task.FromResult(catchProc(e)), continueOnCapturedContext);

        public static async Task<T> Retry<TException, TContext, T>(this Task<T> self, RetryContext<TContext> retryContext, Func<TException, RetryContext<TContext>, Task<T>> retryProc)
            where TException : Exception
        {
            do
            {
                try
                {
                    return await self.ConfigureAwait(retryContext.ContinueOnCapturedContext);
                }
                catch (TException e)
                {
                    retryContext.ErrorCount++;
                    self = retryProc(e, retryContext);
                }
            }
            while (!retryContext.Break);

            return await self.ConfigureAwait(retryContext.ContinueOnCapturedContext);
        }

        public static Task Create<T>(Func<CancellationToken, Task<T>> createProc, TimeSpan timeout)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            return createProc(cts.Token);
        }
    }
}
