using System.Collections.Generic;
using System.Linq;
using TechTalk.SpecFlow;
using AHelper.SlnMerge.Core.Tests.Drivers;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace AHelper.SlnMerge.Core.Tests.StepDefinitions
{
    [Binding]
    public class Steps
    {
        private readonly Driver _driver;

        public Steps(Driver driver, ITestOutputHelper outputHelper)
        {
            _driver = driver;
            _driver.OutputHelper = outputHelper;
        }

        [Given(@"test projects created with ""(.*)""")]
        public void GivenTestGeneratorScriptName(string filename)
            => _driver.GenerateProjects(filename);

        [Given(@"test project ""(.*)"" created with ""(.*)""")]
        public void GivenTestProjectAndGeneratorScriptName(string project, string filename)
        {
            _driver.GenerateProjects(filename);
            _driver.SetTestProject(project);
        }

        [Given(@"a test project ""(.*)""")]
        public void GivenTestProject(string name)
            => _driver.SetTestProject(name);

        [When(@"merging solutions: (.*)")]
        public Task WhenMergeSolutionsAsync(string names)
            => _driver.MergeSolutionsAsync(Split(names), true);

        [When(@"merging solutions with exceptions: (.*)")]
        public Task WhenMergeSolutionsWithExceptionsAsync(string names)
            => _driver.MergeSolutionsAsync(Split(names), false);

        [Then(@"project (.*) should reference (.*)")]
        public void ThenCheckReferences(string project, string references)
            => _driver.CheckReferences(project, Split(references));

        [Then(@"solution (.*) should include (.*)")]
        public void ThenCheckSolution(string solution, string projects)
            => _driver.CheckSolution(solution, Split(projects));

        [Then("it should not throw any exceptions")]
        public void ThenCheckNoExceptions()
            => _driver.CheckNoExceptions();

        [Then(@"it should throw a CyclicReferenceException with projects (.*)")]
        public void ThenCheckCyclicReferenceException(string projects)
            => _driver.CheckCyclicReferenceException(SplitArrows(projects).ToList());

        [Then(@"it should print (warning|error) FileReadException for '(.*)'")]
        public void ThenCheckFileNotFoundException(string severity, string path)
            => _driver.CheckFileNotFoundException(path, severity == "error");

        [Then(@"it should throw an AmbiguousProjectException with package id '(.*)'")]
        public void ThenCheckAmbiguousProjectException(string packageId)
            => _driver.CheckAmbiguousProjectException(packageId);

        private IEnumerable<string> Split(string str)
            => str.Split(',').Select(name => name.Trim());

        private IEnumerable<string> SplitArrows(string str)
            => str.Split("->").Select(name => name.Trim());
    }
}
