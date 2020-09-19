using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AHelper.SlnMerge
{
    internal static class EnumerableExtensions
    {
        public static Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> enumerable)
            => Task.WhenAll(enumerable).ContinueWith(task => task.Result.AsEnumerable());

        public static Task WhenAll(this IEnumerable<Task> enumerable)
            => Task.WhenAll(enumerable);

        public static Task WhenAll(this Task<IEnumerable<Task>> enumerable)
            => enumerable.ContinueWith(value => Task.WhenAll(value));

        public static Task WhenAll<T>(this IEnumerable<Task<T>> enumerable, Action<IEnumerable<T>> predicate)
            => Task.WhenAll(enumerable).ContinueWith(task => predicate(task.Result));

        public static Task<IEnumerable<TOut>> WhenAll<TIn, TOut>(this IEnumerable<Task<TIn>> enumerable, Func<IEnumerable<TIn>, IEnumerable<TOut>> predicate)
            => Task.WhenAll(enumerable).ContinueWith(task => predicate(task.Result));

        public static Task<List<T>> ToListAsync<T>(this Task<IEnumerable<T>> enumerable)
            => enumerable.ContinueWith(task => task.Result.ToList());

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> predicate)
        {
            foreach (var element in enumerable)
            {
                predicate(element);
            }
        }
    }
}
