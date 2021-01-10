using System;

namespace AHelper.SlnMerge.Core
{
    public class FileReadException : Exception
    {
        public FileReadExceptionType FileType { get; set; }
        public string FilePath { get; set; }
        public string ReferencedBy { get; set; }

        public FileReadException(FileReadExceptionType fileType, string filePath, string referencedBy)
            : base(GetMessage(fileType))
        {
            FileType = fileType;
            FilePath = filePath;
            ReferencedBy = referencedBy;
        }

        private static string GetMessage(FileReadExceptionType fileType)
            => fileType switch
            {
                FileReadExceptionType.ProjectReference => "Project reference is not in the solution",
                FileReadExceptionType.Csproj => "Project could not be found",
                FileReadExceptionType.Nuspec => "Nuspec file could not be found",
                FileReadExceptionType.Sln => "Solution could not be found",
                _ => "File could not be read"
            };

        public override string ToString()
            => ReferencedBy switch
            {
                not null => $"{Message}: {FilePath}\n  Referenced by: {ReferencedBy}",
                null => $"{Message}: {FilePath}"
            };
    }
}