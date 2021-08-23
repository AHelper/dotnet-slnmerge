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

        [Given(@"test project created with ""(.*)""")]
        public void GivenTestGeneratorScriptName(string filename)
            => _driver.GenerateProjects(filename);

        [Given(@"nugets created for solution ""(.*)"" with version ""(.*)""")]
        public void GivenNugetsGenerated(string solution, string version)
            => _driver.GenerateNugets(solution, version);

        [Given(@"a test project ""(.*)""")]
        public void GivenTestProject(string name)
            => _driver.SetTestProject(name);

        [When(@"merging solutions with restoring: (.*)")]
        public Task WhenMergeSolutionsWithRestoreAsync(string names)
            => _driver.MergeSolutionsAsync(Split(names), true, true);

        [When(@"merging solutions: (.*)")]
        public Task WhenMergeSolutionsAsync(string names)
            => _driver.MergeSolutionsAsync(Split(names), true, false);

        [When(@"merging solutions in solution folder '(.*)': (.*)")]
        public Task WhenMergeSolutionsAsync(string solutionFolder, string names)
            => _driver.MergeSolutionsAsync(Split(names), true, false, solutionFolder);

        [When(@"merging the local solution with: (.*)")]
        public Task WhenMergeLocalSolutionAsync(string paths)
            => _driver.MergeLocalSolutionsAsync(Split(paths), true, false);

        [When(@"merging the local solution with exceptions: (.*)")]
        public Task WhenMergeLocalSolutionWithExceptionsAsync(string paths)
            => _driver.MergeLocalSolutionsAsync(Split(paths), false, false);

        [When(@"merging solutions with exceptions: (.*)")]
        public Task WhenMergeSolutionsWithExceptionsAsync(string names)
            => _driver.MergeSolutionsAsync(Split(names), false, false);

        [When(@"undoing merges in solutions: (.*)")]
        public Task WhenUndoingMergesInSolutions(string paths)
            => _driver.UndoMergesAsync(Split(paths));

        [Then(@"project (.*) should reference ([^\s]*)")]
        public void ThenCheckReferences(string project, string references)
            => _driver.CheckReferences(project, Split(references));

        [Then(@"project (.*) should not reference ([^\s]*)")]
        public void ThenCheckNotReferenced(string project, string references)
            => _driver.CheckNotReferenced(project, Split(references));

        [Then(@"project (.*) should reference ([^\s]*) for framework ([^\s]*)")]
        public void ThenCheckReferences(string project, string references, string framework)
            => _driver.CheckReferences(project, Split(references), framework);

        [Then(@"project (.*) should have (.*) item groups")]
        public void ThenProjectAAA_CsprojShouldHaveItemGroups(string project, int numItemGroups)
            => _driver.CheckNumberOfItemGroups(project, numItemGroups);

        [Then(@"solution (.*) should include (.*)")]
        public void ThenCheckSolution(string solution, string projects)
            => _driver.CheckSolution(solution, Split(projects));

        [Then(@"solution (.*) should not include (.*)")]
        public void ThenCheckNotInSolution(string solution, string projects)
            => _driver.CheckProjectsNotInSolution(solution, Split(projects));

        [Then(@"solution (.*) should have project paths (.*)")]
        public void ThenCheckSolutionHasProjectPaths(string solution, string projects)
            => _driver.CheckSolutionHasProjectPaths(solution, Split(projects));

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

        [Then(@"it should throw an AmbiguousSolutionException with solutions: (.*)")]
        public void ThenCheckAmbigiousSolutionException(string solutions)
            => _driver.CheckAmbiguousSolutionException(Split(solutions));

        private IEnumerable<string> Split(string str)
            => str.Split(',').Select(name => name.Trim());

        private IEnumerable<string> SplitArrows(string str)
            => str.Split("->").Select(name => name.Trim());
    }
}
