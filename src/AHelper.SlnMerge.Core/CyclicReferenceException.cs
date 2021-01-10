using System;
using System.Collections.Generic;
using System.Linq;

namespace AHelper.SlnMerge.Core
{
    public class CyclicReferenceException : Exception
    {
        public IList<Project> Projects { get; }
        
        public CyclicReferenceException(IList<Project> projects)
            : base("A cyclic dependency was detected between the following projects")
        {
            Projects = projects;
        }

        public override string ToString()
            => $"{Message}:\n{string.Join("\n", Projects.Select(proj => $"-> {proj.Filepath}"))}";
    }
}
