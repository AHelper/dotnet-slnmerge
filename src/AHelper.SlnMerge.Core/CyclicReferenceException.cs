using System;
using System.Collections.Generic;

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
    }
}
