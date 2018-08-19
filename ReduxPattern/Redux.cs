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
    
    public static class Redux
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
    }
}
