using System;
using System.Diagnostics;

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
    }
}