using Microsoft.Build.Evaluation;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using MSBuildProject = Microsoft.Build.Evaluation.Project;

namespace AHelper.SlnMerge.Core.Tests.Drivers
{
    public class Driver
    {
        public ITestOutputHelper OutputHelper { get; set; }
        private string _projectPath;
        private readonly Mock<IOutputWriter> _outputWriterMock;

        private Exception OutputException => _outputWriterMock.Invocations.FirstOrDefault(i => i.Method.Name == nameof(IOutputWriter.PrintException))?.Arguments[0] as Exception;
        private Exception OutputWarning => _outputWriterMock.Invocations.FirstOrDefault(i => i.Method.Name == nameof(IOutputWriter.PrintWarning))?.Arguments[0] as Exception;

        public Driver()
        {
            _outputWriterMock = new Mock<IOutputWriter>();
            _outputWriterMock.Setup(x => x.PrintTrace(It.IsAny<string>(), It.IsAny<object[]>()))
                             .Callback((string format, object[] args) => OutputHelper?.WriteLine(format, args));
            _outputWriterMock.Setup(x => x.PrintException(It.IsAny<Exception>()))
                             .Callback((Exception ex) => OutputHelper?.WriteLine(ex.ToString()));
        }

        public void GenerateProjects(string filename)
            => RunProcess("pwsh", "-File", filename);

        public void SetTestProject(string name)
            => _projectPath = Path.Join("Resources", name);

        public Task MergeSolutionsAsync(IEnumerable<string> solutions, bool shouldAssert)
            => MergeSolutionsRawAsync(solutions.Select(sln => Path.Join(_projectPath, sln)).ToList(), shouldAssert);

        public Task MergeLocalSolutionsAsync(IEnumerable<string> paths, bool shouldAssert)
            => MergeSolutionsRawAsync(paths.Select(sln => Path.Join(_projectPath, sln)).Append($"{_projectPath}/.").ToList(), shouldAssert);

        public async Task MergeSolutionsRawAsync(IList<string> solutions, bool shouldAssert)
        {
            try
            {
                await new Runner(_outputWriterMock.Object).RunAsync(new RunnerOptions
                {
                    Solutions = solutions
                });
            }
            catch (Exception ex)
            {
                Assert.Null(ex);
            }

            if (shouldAssert)
            {
                Assert.Null(OutputException);
            }
        }

        public void CheckReferences(string projectPath, IEnumerable<string> references)
        {
            using var projectCollection = new ProjectCollection();
            var project = new MSBuildProject(Path.Combine(_projectPath, projectPath), new Dictionary<string, string>(), null, projectCollection);
            var items = project.GetItems("ProjectReference");
            var actualReferences = items.Select(item => item.EvaluatedInclude).Select(NormalizePaths).ToList();
            Assert.All(references, reference => Assert.Contains(reference, actualReferences));
            Assert.All(references, reference => Assert.Equal("slnmerge", items.FirstOrDefault(item => NormalizePaths(item.EvaluatedInclude) == reference)?.GetMetadataValue("Origin")));
        }

        public void CheckReferences(string projectPath, IEnumerable<string> references, string framework)
        {
            using var projectCollection = new ProjectCollection();
            var project = new MSBuildProject(Path.Combine(_projectPath, projectPath),
                                                                 new Dictionary<string, string>(),
                                                                 null,
                                                                 projectCollection);
            var targetFrameworksValue = project.GetProperty("TargetFrameworks")?.EvaluatedValue ?? project.GetPropertyValue("TargetFramework");

            foreach (var targetFramework in targetFrameworksValue.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                project.SetGlobalProperty("TargetFramework", targetFramework);
                project.ReevaluateIfNecessary();

                var items = project.GetItems("ProjectReference");
                var referenceItems = items.Select(item => item.EvaluatedInclude).Select(NormalizePaths);

                if (targetFramework == framework)
                {
                    Assert.All(references, reference => Assert.Contains(reference, referenceItems));
                    Assert.All(references, reference => Assert.Equal("slnmerge", items.FirstOrDefault(item => NormalizePaths(item.EvaluatedInclude) == reference)?.GetMetadataValue("Origin")));
                }
                else
                {
                    Assert.All(references, reference => Assert.DoesNotContain(reference, referenceItems));
                }
            }
        }

        public void CheckSolution(string solution, IEnumerable<string> projects)
        {
            var actualProjects = RunProcess("dotnet", "sln", solution, "list").Skip(2).Select(NormalizePaths).ToList();
            Assert.All(projects, project => Assert.Contains(project, actualProjects));
        }

        public void CheckAmbiguousSolutionException(IEnumerable<string> solutionPaths)
        {
            Assert.IsType<AmbiguousSolutionException>(OutputException);

            var ambiguousSolutionException = (AmbiguousSolutionException)OutputException;

            Assert.All(solutionPaths, sln => Assert.Contains(NormalizeToProjectPath(sln), ambiguousSolutionException.Paths.Select(NormalizePaths)));
        }

        public void CheckCyclicReferenceException(IList<string> packageIds)
        {
            Assert.IsType<CyclicReferenceException>(OutputException);

            var cyclicException = (CyclicReferenceException)OutputException;

            Assert.Equal(packageIds, cyclicException.Projects.Select(proj => proj.PackageId));
        }

        public void CheckFileNotFoundException(string path, bool isError)
        {
            var exception = isError ? OutputException : OutputWarning;
            Assert.IsType<FileReadException>(exception);

            var fileReadException = exception as FileReadException;

            Assert.Equal(NormalizePaths(Path.GetFullPath(Path.Join(_projectPath, path))), NormalizePaths(fileReadException.FilePath));
        }

        public void CheckAmbiguousProjectException(string packageId)
        {
            Assert.IsType<AmbiguousProjectException>(OutputException);

            var ambiguousProjectException = OutputException as AmbiguousProjectException;
            Assert.Contains(packageId, ambiguousProjectException.Conflicts.Keys);
        }

        public void CheckNoExceptions()
        {
            _outputWriterMock.Verify(writer => writer.PrintException(It.IsAny<Exception>()), Times.Never());
        }

        public void CheckHandledException<TException>() where TException : Exception
        {
            _outputWriterMock.Verify(writer => writer.PrintException(It.IsAny<TException>()), Times.Once());
        }

        private IEnumerable<string> RunProcess(string filename, params string[] arguments)
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filename,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = _projectPath ?? "Resources"
                }
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);

            return output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string NormalizePaths(string path)
            => path.Replace('\\', '/');

        private string NormalizeToProjectPath(string relativePath)
            => NormalizePaths(Path.GetFullPath(Path.Join(_projectPath, relativePath)));
    }
}
