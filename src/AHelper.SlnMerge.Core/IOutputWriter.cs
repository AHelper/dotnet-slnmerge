using System;
using System.Collections.Generic;

namespace AHelper.SlnMerge.Core
{
    public interface IOutputWriter
    {
        void PrintException(Exception exception);
        void PrintComplete(int numModified);
        void PrintArgumentMessage(string message);
        void PrintProgress(string file);
        void PrintCommand(string command);
        void PrintWarning(Exception exception);
        void PrintTrace(string format, params object[] args);
    }
}