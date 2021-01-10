using Xunit;

namespace AHelper.SlnMerge.Core.Tests.UnitTests
{
    public class CyclicReferenceExceptionTests
    {
        [Fact]
        public void TestToString()
        {
            const string Filepath = "path";
            var exception = new CyclicReferenceException(new[]{
                Project.CreateForTesting(Filepath, "Test")
            });

            Assert.Contains("cyclic ", exception.ToString());
            Assert.Contains("\n", exception.ToString());
            Assert.Contains(Filepath, exception.ToString());
        }
    }
}