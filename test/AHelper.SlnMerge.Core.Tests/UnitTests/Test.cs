using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AHelper.SlnMerge.Core.Tests.UnitTests
{
    public class TestClass
    {
        [Fact]
        public async Task Test()
        {
            var outputMock = new Mock<IOutputWriter>();
            await new Runner(outputMock.Object).RunAsync(new RunnerOptions
            {
                Solutions = new[] { "Resources/Test" }
            });
        }
    }
}
