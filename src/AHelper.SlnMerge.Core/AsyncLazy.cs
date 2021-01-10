using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AHelper.SlnMerge.Core
{
    public class AsyncLazy<T> : Lazy<Task<T>>
    {
        public AsyncLazy(Func<T> factory)
            : base(() => Task.Factory.StartNew(factory))
        { }

        public AsyncLazy(Func<Task<T>> factory)
            : base(() => Task.Factory.StartNew(factory).Unwrap())
        { }

        public TaskAwaiter<T> GetAwaiter()
            => Value.GetAwaiter();
    }
}
