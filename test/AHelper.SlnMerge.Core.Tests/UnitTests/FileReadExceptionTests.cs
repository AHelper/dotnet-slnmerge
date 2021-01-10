using Xunit;

namespace AHelper.SlnMerge.Core.Tests.UnitTests
{
    public class FileReadExceptionTests
    {
        [Fact]
        public void TestCsprojToString()
        {
            var exception = new FileReadException(FileReadExceptionType.Csproj, "path", "referenced");

            Assert.Contains("not be found", exception.ToString());
            Assert.Contains("path", exception.ToString());
            Assert.Contains("referenced", exception.ToString());
        }
        
        [Fact]
        public void TestNuspecToString()
        {
            var exception = new FileReadException(FileReadExceptionType.Nuspec, "path", "referenced");

            Assert.Contains("not be found", exception.ToString());
            Assert.Contains("path", exception.ToString());
            Assert.Contains("referenced", exception.ToString());
        }

        [Fact]
        public void TestSlnToString()
        {
            var exception = new FileReadException(FileReadExceptionType.Sln, "path", "referenced");

            Assert.Contains("not be found", exception.ToString());
            Assert.Contains("path", exception.ToString());
            Assert.Contains("referenced", exception.ToString());
        }

        [Fact]
        public void TestProjectReferenceToString()
        {
            var exception = new FileReadException(FileReadExceptionType.ProjectReference, "path", "referenced");

            Assert.Contains("not in the solution", exception.ToString());
            Assert.Contains("path", exception.ToString());
            Assert.Contains("referenced", exception.ToString());
        }
    }
}