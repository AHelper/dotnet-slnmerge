using System;
using System.Collections.Generic;

namespace AHelper.SlnMerge.Core
{
    public class AmbiguousProjectException : Exception
    {
        public IDictionary<string /* packageId */, IEnumerable<string> /* projects */> Conflicts { get; }

        public AmbiguousProjectException(IDictionary<string, IEnumerable<string>> conflicts)
            : base("Multiple projects have the same PackageId")
        {
            Conflicts = conflicts;
        }
    }
}
