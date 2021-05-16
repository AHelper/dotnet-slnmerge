using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AHelper.SlnMerge.Core
{
    public class ConsoleOutputWriter : IOutputWriter
    {
        public TraceLevel LogLevel { get; set; } = TraceLevel.Info;

        public void PrintArgumentMessage(string message)
        {
            Console.WriteLine(message);
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

        public void PrintInfo(string message)
        {
            Print(TraceLevel.Info, message);
        }

        public void PrintProgress(string file)
        {
            Print(TraceLevel.Verbose, file);
        }

        public void PrintTrace(string format, params object[] args)
        {
            Print(TraceLevel.Verbose, string.Format(format, args));
        }

        public void PrintWarning(Exception exception)
        {
            Print(TraceLevel.Warning, exception.ToString());
        }

        public Task StartProgressContext(RunnerOptions options, Func<IProgressContext, Task> predicate)
            => predicate(new NullProgressContext());

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
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.Write("ERROR");
                    break;
                case TraceLevel.Warning:
                    Console.BackgroundColor = ConsoleColor.Yellow;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("WARN");
                    break;
                case TraceLevel.Info:
                    Console.BackgroundColor = ConsoleColor.Green;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write("=>");
                    break;
                case TraceLevel.Verbose:
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.ForegroundColor = ConsoleColor.Black;
                    break;
            }

            var foreground = Console.BackgroundColor;
            Console.ResetColor();
            Console.Write(" ");
            Console.ForegroundColor = foreground;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}