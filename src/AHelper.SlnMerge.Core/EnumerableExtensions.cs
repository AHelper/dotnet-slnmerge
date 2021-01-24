using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AHelper.SlnMerge.Core
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

        public static Task<TOut> WhenAll<TIn, TOut>(this IEnumerable<Task<TIn>> enumerable, Func<IEnumerable<TIn>, TOut> predicate)
            => Task.WhenAll(enumerable).ContinueWith(task => predicate(task.Result));

        public static Task<List<T>> ToListAsync<T>(this Task<IEnumerable<T>> enumerable)
            => enumerable.ContinueWith(task => task.Result.ToList());

        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<T, TKey, TValue>(this Task<IEnumerable<T>> enumerable, Func<T, TKey> keyPredicate, Func<T, TValue> valuePredicate)
            => enumerable.ContinueWith(task => task.Result.ToDictionary(keyPredicate, valuePredicate));

        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> enumerable, Func<T, TKey> predicate)
        {
            var keys = new HashSet<TKey>();

            foreach (var item in enumerable)
            {
                var key = predicate(item);
                if (!keys.Contains(key))
                {
                    keys.Add(key);
                    yield return item;
                }
            }
        }

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> predicate)
        {
            foreach (var element in enumerable)
            {
                predicate(element);
            }
        }

        public static IEnumerable<T> Expand<T>(this T item, Func<T, T> predicate)
        {
            while (item != null)
            {
                yield return item;
                item = predicate(item);
            }
        }
    }
}
