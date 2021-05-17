using Spectre.Console;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AHelper.SlnMerge.Core
{
    public class SpectreConsoleOutputWriter : IOutputWriter
    {
        public TraceLevel LogLevel { get; set; } = TraceLevel.Info;

        public void PrintArgumentMessage(string message)
        {
            AnsiConsole.WriteLine(message);
        }

        public void PrintCommand(string command)
        {
            Print(TraceLevel.Verbose, command);
        }

        public void PrintComplete(int numModified)
        {
            if (numModified == 0)
            {
                Print(TraceLevel.Info, "All projects merged");
            }
            else
            {
                Print(TraceLevel.Info, $"{numModified} projects merged");
            }
        }

        public void PrintException(Exception exception)
        {
            Print(TraceLevel.Error, exception.ToString());
        }

        public void PrintProgress(string file)
        {
            Print(TraceLevel.Verbose, file);
        }

        public void PrintInfo(string message)
        {
            Print(TraceLevel.Info, message);
        }

        public void PrintTrace(string format, params object[] args)
        {
            Print(TraceLevel.Verbose, string.Format(format, args));
        }

        public void PrintWarning(Exception exception)
        {
            Print(TraceLevel.Warning, exception.ToString());
        }

        private void Print(TraceLevel level, string message)
        {
            if (level > LogLevel)
            {
                return;
            }

            switch (level)
            {
                case TraceLevel.Off:
                    return;
                case TraceLevel.Error:
                    PrintWrapped($"[red]ERR: [/]{message.EscapeMarkup()}");
                    break;
                case TraceLevel.Warning:
                    PrintWrapped($"[yellow]WARN: [/]{message.EscapeMarkup()}");
                    break;
                case TraceLevel.Info:
                    PrintWrapped($"[green]=> [/]{message.EscapeMarkup()}");
                    break;
                case TraceLevel.Verbose:
                    PrintWrapped($"[grey]DBG: [/]{message.EscapeMarkup()}");
                    break;
            }
        }

        private static void PrintWrapped(string markup)
        {
            var start = 0;
            var isInTag = false;
            var index = 0;
            var length = 0;

            for (; index < markup.Length; index++)
            {
                var c = markup[index];
                var next = index != markup.Length - 1 ? markup[index + 1] : (char?)null;

                if (c == '[' && next != '[')
                {
                    isInTag = true;
                }
                else if (isInTag && c == ']' && next != ']')
                {
                    isInTag = false;
                    continue;
                }

                if (!isInTag)
                {
                    length++;

                    if (length == AnsiConsole.Profile.Width)
                    {
                        // Print chunk
                        AnsiConsole.MarkupLine(markup.Substring(start, index - start + 1));
                        start = index + 1;
                        length = 0;
                    }
                }
            }

            if (start != index)
            {
                AnsiConsole.MarkupLine(markup.Substring(start, index - start));
            }
        }

        public Task StartProgressContext(RunnerOptions options, Func<IProgressContext, Task> predicate)
            => AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .AutoClear(options.Verbosity != TraceLevel.Verbose)
                    .Columns(
                        new TaskDescriptionColumn() { Alignment = Justify.Left },
                        new ProgressBarColumn() { Width = null },
                        new PercentageColumn(),
                        new ElapsedTimeColumn(),
                        new SpinnerColumn(Spinner.Known.Dots12)
                    )
                    .StartAsync(ctx => predicate(new SpectreProgressContext(ctx)));
    }

    public class SpectreProgressContext : IProgressContext
    {
        private readonly ProgressContext _ctx;

        public SpectreProgressContext(ProgressContext ctx)
        {
            _ctx = ctx;
        }

        public IProgressTask AddTask(string description)
            => new SpectreProgressTask(_ctx.AddTask(description));
    }

    public class SpectreProgressTask : IProgressTask
    {
        private readonly ProgressTask _task;

        public SpectreProgressTask(ProgressTask task)
        {
            _task = task;
        }

        public void Increment(double value) 
            => _task.Increment(value);

        public void StopTask()
        {
            _task.Value = _task.MaxValue;
            _task.StopTask();
        }
    }
}