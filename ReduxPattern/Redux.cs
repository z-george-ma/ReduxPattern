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
    }

    public class ActionState<TAction, TState>
    {
        internal TAction Action { get; set; }
        internal TState OldState { get; set; }
        internal TState NewState { get; set; }
    }

    public static class Extensions
    {
        public static ActionStore<TAction, TState> Use<TAction, TState>(this TAction self, IStore<TState> store) =>
            new ActionStore<TAction, TState>
            {
                Action = self,
                Store = store
            };

        public static async Task<ActionState<TAction, TState>> Reduce<TAction, TState>(this ActionStore<TAction, TState> self, Func<TState, TAction, TState> reducer, bool continueOnCapturedContext = false)
        {
            var state = await self.Store.GetState().ConfigureAwait(continueOnCapturedContext);

            var newState = reducer(state, self.Action);

            await self.Store.SaveState(newState, state).ConfigureAwait(continueOnCapturedContext);

            return new ActionState<TAction, TState>
            {
                Action = self.Action,
                OldState = state,
                NewState = newState
            };
        }

        public static async Task<ActionState<TAction, TState>> Reduce<TAction, TState>(this ActionStore<Task<TAction>, TState> self, Func<TState, TAction, TState> reducer, bool continueOnCapturedContext = false) =>
            await new ActionStore<TAction, TState>
            {
                Action = await self.Action.ConfigureAwait(continueOnCapturedContext),
                Store = self.Store
            }.Reduce(reducer, continueOnCapturedContext);

        public static async Task<TResult> Effect<TAction, TState, TResult>(this Task<ActionState<TAction, TState>> self, Func<TState, TState, TAction, Task<TResult>> effector, bool continueOnCapturedContext = false)
        {
            var actionState = await self.ConfigureAwait(continueOnCapturedContext);
            return await effector(actionState.OldState, actionState.NewState, actionState.Action).ConfigureAwait(continueOnCapturedContext);
        }

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
    }
}
