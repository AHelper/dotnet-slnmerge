using System;

namespace AHelper.SlnMerge.Core
{
    public class FileReadException : Exception
    {
        public FileReadExceptionType FileType { get; set; }
        public string FilePath { get; set; }
        public string ReferencedBy { get; set; }

        public FileReadException(string message)
            : base(message)
        {
        }
    }
}