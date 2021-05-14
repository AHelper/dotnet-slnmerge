using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AHelper.SlnMerge.Core
{
    public class Solution
    {
        public AsyncLazy<List<Project>> Projects { get; private set; }
        public IDictionary<Project, ChangeType> Changes { get; } = new ConcurrentDictionary<Project, ChangeType>();
        public string Filepath { get; }
        private static object _buildLock = new();

        private readonly IOutputWriter _outputWriter;

        public Solution(IOutputWriter outputWriter, string filepath)
        {
            if (Directory.Exists(filepath))
            {
                var solutions = Directory.GetFiles(filepath, "*.sln");

                if (solutions.Length > 1)
                {
                    throw new AmbiguousSolutionException(solutions.Select(Path.GetFullPath).ToList());
                }

                filepath = solutions.FirstOrDefault() ?? filepath;
            }

            if (!File.Exists(filepath))
            {
                throw new FileReadException(FileReadExceptionType.Sln, Path.GetFullPath(filepath), null);
            }

            Filepath = Path.GetFullPath(filepath);
            Projects = new AsyncLazy<List<Project>>(GetProjectsAsync);
            _outputWriter = outputWriter;
        }

        public void RestoreNugets(RunnerOptions options)
        {
            lock (_buildLock)
            {
                _outputWriter.PrintProgress($"Restoring nugets for {Filepath}...");
                var properties = new Dictionary<string, string>
                {
                    ["Configuration"] = "Debug",
                    ["Platform"] = "Any CPU"
                };
                var parameters = new BuildParameters
                {
                    ProjectLoadSettings = ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports
                };

                if (options.Verbosity >= TraceLevel.Verbose)
                {
                    var logger = new Microsoft.Build.Logging.BinaryLogger()
                    {
                        Parameters = $"{Filepath}.binlog"
                    };
                    parameters.Loggers = new[] { logger };
                }

                var result = BuildManager.DefaultBuildManager.Build(parameters,
                                                                    new BuildRequestData(Filepath, properties, null, new[] { "Restore" }, null));

                if (result.Exception != null)
                {
                    _outputWriter.PrintWarning(result.Exception);
                }
                else if (result.ResultsByTarget["Restore"].Exception != null)
                {
                    _outputWriter.PrintWarning(result.ResultsByTarget["Restore"].Exception);
                }
                if (result.OverallResult == BuildResultCode.Failure)
                {
                    _outputWriter.PrintWarning(new Exception($"Restoring packages for {Filepath} failed"));
                }
            }
        }

        public async Task CheckDetachedProjectReferences()
        {
            var projs = await Projects.Value;
            var filepaths = projs.Select(proj => proj.Filepath).ToList();

            foreach (var proj in projs)
            {
                foreach (var projectRef in proj.ProjectReferences)
                {
                    var refPath = Path.GetFullPath(projectRef.Include, Path.GetDirectoryName(proj.Filepath));

                    if (!File.Exists(refPath))
                    {
                        throw new FileReadException(FileReadExceptionType.Csproj, refPath, proj.Filepath);
                    }

                    if (!filepaths.Contains(refPath))
                    {
                        _outputWriter.PrintWarning(new FileReadException(FileReadExceptionType.ProjectReference, refPath, proj.Filepath));
                    }
                }
            }
        }

        public async Task PruneProjectsAsync(Workspace workspace)
        {
            var projects = await Projects.Value;
            var removedProjects = projects.SelectMany(proj => proj.Changes)
                                          .Where(change => change.ChangeType == ChangeType.Removed)
                                          .Select(change => change.Project)
                                          .Distinct();
            var referencedProjects = projects.Except(removedProjects)
                                             .SelectMany(proj => proj.GetProjectReferences(workspace))
                                             .Distinct();

            var open = new Queue<Project>(removedProjects.Except(referencedProjects));
            var remaining = new List<Project>(projects);
            var toBeRemoved = new List<Project>();

            while (open.Any())
            {
                var proj = open.Dequeue();
                if (!remaining.Remove(proj))
                {
                    continue;
                }

                Changes.Add(new KeyValuePair<Project, ChangeType>(proj, ChangeType.Removed));

                foreach (var projRef in proj.GetProjectReferences(workspace))
                {
                    if (!remaining.Any(r => r.GetProjectReferences(workspace).Contains(projRef)))
                    {
                        open.Enqueue(projRef);
                    }
                }
            }
        }

        internal async Task ReplaceProjects(IEnumerable<Project> projects)
        {
            var myProjects = await Projects.Value;

            Projects = new AsyncLazy<List<Project>>(() => myProjects.Join(projects, proj => proj.Filepath, proj => proj.Filepath, (_, p) => p)
                                                                    .ToList());
        }

        private IList<string> GetProjectPaths()
            => SolutionFile.Parse(Filepath).ProjectsInOrder
                           .Where(proj => proj.ProjectType != SolutionProjectType.SolutionFolder)
                           .Select(proj => proj.RelativePath)
                           .ToList();

        private Task<List<Project>> GetProjectsAsync()
            => GetProjectPaths().Select(path => Project.CreateAsync(path, this, _outputWriter))
                                .WhenAll()
                                .ToListAsync();
    }
}
