using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AHelper.SlnMerge.Core
{
    public interface IOutputWriter
    {
        TraceLevel LogLevel { get; set; }
        
        void PrintException(Exception exception);
        void PrintComplete(int numModified);
        void PrintArgumentMessage(string message);
        void PrintProgress(string file);
        void PrintCommand(string command);
        void PrintWarning(Exception exception);
        void PrintTrace(string format, params object[] args);
        Task StartProgressContext(RunnerOptions options, Func<IProgressContext, Task> predicate);
    }

    public interface IProgressContext
    {
        IProgressTask AddTask(string description);
    }

    public interface IProgressTask
    {
        void StopTask();
        void Increment(double value);
    }
}