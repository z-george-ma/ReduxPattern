using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ReduxPattern
{
    public interface IStore<TState>
    {
        Task<TState> GetState();
        Task SaveState(TState state, TState oldState);
    }

    public class ActionStore<TAction, TState>
    {
        internal TAction Action { get; set; }
        internal IStore<TState> Store { get; set; }
        internal bool ContinueOnCapturedContext { get; set; }
    }

    public class ActionState<TAction, TState>
    {
        internal TAction Action { get; set; }
        internal Task<TState> OldState { get; set; }
        internal Task<TState> NewState { get; set; }
        internal bool ContinueOnCapturedContext { get; set; }
    }

    public class RetryContext<T>
    {
        public bool Break = false;
        public bool ContinueOnCapturedContext = false;
        public int ErrorCount = 0;
        public T Value { get; set; }
    }
    
    public static class Extensions
    {
        public static ActionStore<TAction, TState> Use<TAction, TState>(this TAction self, IStore<TState> store, bool continueOnCapturedContext = false) =>
            new ActionStore<TAction, TState>
            {
                Action = self,
                Store = store,
                ContinueOnCapturedContext = continueOnCapturedContext
            };

        public static ActionState<TAction, TState> Reduce<TAction, TState>(this ActionStore<TAction, TState> self, Func<TState, TAction, TState> reducer)
        {
            var stateTask = self.Store.GetState();
            
            return new ActionState<TAction, TState>
            {
                Action = self.Action,
                OldState = stateTask,
                NewState = stateTask
                            .Then((TState x) => reducer(x, self.Action), self.ContinueOnCapturedContext)
                            .Then(async (TState x) =>
                            {
                                await self.Store.SaveState(x, await stateTask.ConfigureAwait(self.ContinueOnCapturedContext)).ConfigureAwait(self.ContinueOnCapturedContext);
                                return x;
                            })
            };
        }
        
        public static async Task<TResult> Effect<TAction, TState, TResult>(this ActionState<TAction, TState> self, Func<TState, TState, TAction, Task<TResult>> effector) =>
            await effector(
                await self.OldState.ConfigureAwait(self.ContinueOnCapturedContext), 
                await self.NewState.ConfigureAwait(self.ContinueOnCapturedContext), 
                self.Action).ConfigureAwait(self.ContinueOnCapturedContext);

        public static async Task<TResult> Then<T, TResult>(this Task<T> self, Func<T, Task<TResult>> thenProc, bool continueOnCapturedContext = false)
        {
            var result = await self.ConfigureAwait(continueOnCapturedContext);

            return await thenProc(result).ConfigureAwait(continueOnCapturedContext);
        }

        public static async Task<TResult> Then<T, TResult>(this Task<T> self, Func<T, TResult> thenProc, bool continueOnCapturedContext = false) =>
            thenProc(await self.ConfigureAwait(continueOnCapturedContext));

        public static async Task<T> Catch<TException, T>(this Task<T> self, Func<TException, Task<T>> catchProc, bool continueOnCapturedContext = false)
            where TException: Exception
        {
            try
            {
                return await self.ConfigureAwait(continueOnCapturedContext);
            }
            catch(TException e)
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

    }
}
