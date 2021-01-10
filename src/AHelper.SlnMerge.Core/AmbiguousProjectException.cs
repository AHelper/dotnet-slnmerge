using System;
using System.Collections.Generic;
using System.Text;

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

        public override string ToString()
            => $"{Message}:\n{FormatAbiguousProjectMessage(Conflicts)}";

        private static string FormatAbiguousProjectMessage(IDictionary<string, IEnumerable<string>> conflicts)
        {
            var output = new StringBuilder();

            foreach (var kvp in conflicts)
            {
                output.AppendFormat("- {0}\n", kvp.Key);

                foreach (var proj in kvp.Value)
                {
                    output.AppendFormat("  - {0}\n", proj);
                }
            }

            return output.ToString();
        }
    }
}
