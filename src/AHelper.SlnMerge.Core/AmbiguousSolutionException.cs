using System;
using System.Collections.Generic;
using System.Linq;

namespace AHelper.SlnMerge.Core
{
    public class AmbiguousSolutionException : Exception
    {
        public IList<string> Paths { get; }

        public AmbiguousSolutionException(IList<string> paths)
            : base("Multiple solutions exist in the folder")
        {
            Paths = paths;
        }

        public override string ToString()
            => $"{Message}:\n{string.Join('\n', Paths.Select(path => $"-> {path}"))}";
    }
}