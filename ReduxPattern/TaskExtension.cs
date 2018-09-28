﻿using ReduxPattern;
using System;
using System.Threading.Tasks;

namespace System.Threading.Tasks
{
    public static partial class TaskExtension
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
    }
}
