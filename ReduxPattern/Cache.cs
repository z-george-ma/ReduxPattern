using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ReduxPattern
{
    public struct CacheValue<T>
    {
        private T value;

        public bool HasValue { get; private set; }

        public T Value
        {
            get
            {
                return value;
            }
            set
            {
                HasValue = true;
                this.value = value;
            }
        }
        
        public static implicit operator CacheValue<T>(T value) => 
            new CacheValue<T>
            {
                Value = value
            };
    }

    public interface ICacheItem<T>
    {
        Task<CacheValue<T>> Get();
        Task Set(T value);
    }

    public interface ICacheItemLock
    {
        Task Acquire();
        Task Release();
    }

    public static class Cache
    {
        public static async Task<T> GetOrAdd<T>(this ICacheItem<T> self, ICacheItemLock lockObj, Func<Task<T>> getProc, bool continueOnCapturedContext = false)
        {
            var cacheValue = await self.Get().ConfigureAwait(continueOnCapturedContext);

            if (cacheValue.HasValue)
                return cacheValue.Value;

            await lockObj.Acquire().ConfigureAwait(continueOnCapturedContext); ;

            try
            {
                cacheValue = await self.Get().ConfigureAwait(continueOnCapturedContext);

                if (cacheValue.HasValue)
                    return cacheValue.Value;

                var value = await getProc().ConfigureAwait(continueOnCapturedContext);

                await self.Set(value).ConfigureAwait(continueOnCapturedContext);

                return value;
            }
            finally
            {
                await lockObj.Release().ConfigureAwait(continueOnCapturedContext);
            }
        }
    }
}
