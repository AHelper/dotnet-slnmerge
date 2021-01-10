using System.IO;
using System;
using System.Diagnostics;

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

        public void PrintProgress(string file)
        {
            Print(TraceLevel.Verbose, file);
        }

        public void PrintTrace(string format, params object[] args)
        {
            Console.WriteLine(format, args);
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
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.Write("!!");
                    break;
                case TraceLevel.Warning:
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("##");
                    break;
                case TraceLevel.Info:
                    Console.BackgroundColor = ConsoleColor.Green;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write("=>");
                    break;
                case TraceLevel.Verbose:
                    Console.BackgroundColor = ConsoleColor.DarkYellow;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("..");
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