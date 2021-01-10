using System.Collections.Generic;
using Xunit;

namespace AHelper.SlnMerge.Core.Tests.UnitTests
{
    public class AmbiguousProjectExceptionTests
    {
        [Fact]
        public void TestToString()
        {
            const string Filepath = "path";
            var exception = new AmbiguousProjectException(new Dictionary<string, IEnumerable<string>>
            {
                ["packageId"] = new[] { Filepath }
            });

            Assert.Contains("have the same ", exception.ToString());
            Assert.Contains("\n", exception.ToString());
            Assert.Contains(Filepath, exception.ToString());
        }
    }
}