using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;

namespace AHelper.SlnMerge.Core
{
    public static class ProgressTaskExtensions
    {
        public static Task IncrementWhenAll(this IProgressTask task, IEnumerable<Task> tasks, double value)
        {
            var tasksList = tasks.ToList();

            return Task.WhenAll(tasksList.Select(async t => { await t; task.Increment(value / tasksList.Count); }));
        }

        public static Task IncrementWhenAll(this IEnumerable<Task> enumerable, IProgressTask task, double value)
        {
            var tasksList = enumerable.ToList();

            return Task.WhenAll(tasksList.Select(async t => { await t; task.Increment(value / tasksList.Count); }));
        }

        public static void IncrementForEach<T>(this IEnumerable<T> enumerable, IProgressTask progress, double value, Action<T> predicate)
        {
            var list = enumerable.ToList();
            var incrementValue = value / list.Count;

            foreach (var item in list)
            {
                predicate(item);
                progress.Increment(incrementValue);
            }
        }

        public static async Task IncrementForEach<T>(this IEnumerable<T> enumerable, IProgressTask progress, double value, Func<T, Task> predicate)
        {
            var list = enumerable.ToList();
            var incrementValue = value / list.Count;

            foreach (var item in list)
            {
                await predicate(item);
                progress.Increment(incrementValue);
            }
        }

        public static void WithTask(this IProgressContext ctx, string description, Action<IProgressTask> predicate)
        {
            var task = ctx.AddTask(description);
            predicate(task);
            task.StopTask();
        }

        public static async Task WithTaskAsync(this IProgressContext ctx, string description, Func<IProgressTask, Task> predicate)
        {
            var task = ctx.AddTask(description);
            await predicate(task);
            task.StopTask();
        }

        public static async Task<T> WithTaskAsync<T>(this IProgressContext ctx, string description, Func<IProgressTask, Task<T>> predicate)
        {
            var task = ctx.AddTask(description);
            var result = await predicate(task);
            task.StopTask();
            return result;
        }
    }
}