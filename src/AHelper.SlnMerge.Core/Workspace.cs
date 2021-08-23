using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Spectre.Console;

namespace AHelper.SlnMerge.Core
{
    public class Workspace
    {
        public List<Solution> Solutions { get; }
        public Dictionary<string /* PackageId */, Project> PackageLookup { get; }

        private readonly IOutputWriter _outputWriter;

        private Workspace(IOutputWriter outputWriter, List<Solution> solutions)
        {
            Solutions = solutions;
            _outputWriter = outputWriter;

            try
            {
                PackageLookup = solutions.SelectMany(sln => sln.Projects.Value.Result)
                                         .GroupBy(proj => proj.Filepath, (path, projs) => projs.First())
                                         .ToDictionary(proj => proj.PackageId, proj => proj);
            }
            catch (ArgumentException)
            {
                var conflicts = solutions.SelectMany(sln => sln.Projects.Value.Result)
                         .GroupBy(proj => proj.PackageId)
                         .Where(projs => projs.Count() > 1)
                         .ToDictionary(kvp => kvp.Key, kvp => kvp.Select(proj => proj.Filepath));

                throw new AmbiguousProjectException(conflicts);
            }
        }

        public static async Task<Workspace> CreateAsync(IOutputWriter outputWriter, IEnumerable<string> solutionPaths, IProgressTask createWorkspaceTask)
        {
            var solutions = solutionPaths.Select(path => new Solution(outputWriter, path))
                                         .ToList();
            await createWorkspaceTask.IncrementWhenAll(solutions.Select(sln => sln.Projects.Value), 33);
            await DeduplicateProjects(solutions, createWorkspaceTask);
            await createWorkspaceTask.IncrementWhenAll(solutions.Select(sln => sln.CheckDetachedProjectReferences()), 34);
            createWorkspaceTask.StopTask();

            return new Workspace(outputWriter, solutions);
        }

        public static async Task DeduplicateProjects(List<Solution> solutions, IProgressTask createWorkspaceTask)
        {
            var projects = await solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(projs => projs)
                                                                 .DistinctBy(proj => proj.Filepath)
                                                                 .ToList());
            
            foreach(var sln in solutions)
            {
                await sln.ReplaceProjects(projects);
                createWorkspaceTask.Increment(33.0 / solutions.Count);
            }
        }

        public async Task AddReferencesAsync(IProgressTask task)
            => await Solutions.Select(sln => sln.Projects.Value)
                              .WhenAll(projs => projs.SelectMany(projs => projs)
                                                     .IncrementForEach(task, 100, proj => proj.AddReferences(this)));

        public async Task RemoveReferencesAsync(IProgressTask task)
            => await Solutions.Select(sln => sln.Projects.Value)
                              .WhenAll(projs => projs.SelectMany(projs => projs)
                                                     .IncrementForEach(task, 100, proj => proj.RemoveReferences(this)));

        public async Task CheckForCircularReferences(IProgressTask task)
        {
            var projects = await Solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(projs => projs)
                                                                 .Distinct())
                                          .ToDictionaryAsync(proj => proj.PackageId,
                                                             proj => proj);

            projects.Values.IncrementForEach(task, 100, project =>
            {
                var rootNode = new ReferenceNode(project, null);
                var open = new List<ReferenceNode>(project.GetProjectReferences(this).Select(proj => new ReferenceNode(proj, rootNode)).Distinct());
                var closed = new List<ReferenceNode>(new[] { rootNode });

                while (open.Any())
                {
                    var openProj = open.First();
                    open.Remove(openProj);
                    closed.Add(openProj);

                    foreach (var projRef in openProj.Project.GetProjectReferences(this))
                    {
                        if (projRef == project)
                        {
                            throw new CyclicReferenceException(new ReferenceNode(projRef, openProj).Expand(node => node.Parent)
                                                                                                   .Select(node => node.Project)
                                                                                                   .Reverse()
                                                                                                   .ToList());
                        }

                        if (!closed.Any(node => node.Project == projRef))
                        {
                            open.Add(new ReferenceNode(projRef, openProj));
                        }
                    }
                }
            });
        }

        public void RestoreNugets(RunnerOptions options, IProgressTask task)
        {
            if (options.NoRestore)
            {
                task.Increment(100);
                return;
            }

            Solutions.IncrementForEach(task, 100, sln => sln.RestoreNugets(options));
        }

        public async Task AddTransitiveReferences(RunnerOptions options, IProgressTask task)
        {
            var projects = await Solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(proj => proj)
                                                                 .Distinct());

            projects.IncrementForEach(task, 100, project =>
            {
                project.AddTransitiveReferences(this, options);
            });
        }

        public async Task AddVersionsAsync(IProgressTask task)
        {
            var projects = await Solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(proj => proj)
                                                                 .Distinct());

            await projects.IncrementForEach(task, 100, project => project.AddVersionOverrideAsync(this));
        }

        public async Task RemoveVersionsAsync(IProgressTask task)
        {
            var projects = await Solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(proj => proj)
                                                                 .Distinct());

            await projects.IncrementForEach(task, 100, project => project.RemoveVersionOverrideAsync());
        }

        public Task PopulateSolutionsAsync(IProgressTask task)
            => Solutions.IncrementForEach(task, 100, async sln =>
            {
                var projects = await sln.Projects;
                var closed = new List<Project>();
                var open = new Stack<Project>(projects);

                while (open.TryPop(out var proj))
                {
                    if (closed.Contains(proj))
                    {
                        continue;
                    }

                    closed.Add(proj);

                    if (!projects.Contains(proj))
                    {
                        sln.Changes[proj] = ChangeType.Added;
                    }

                    proj.GetProjectReferences(this)
                        .ForEach(open.Push);
                }
            });

        public Task CleanupSolutionsAsync(IProgressTask task)
            => Solutions.Select(sln => sln.PruneProjectsAsync(this))
                        .IncrementWhenAll(task, 100);

        public async Task CommitChangesAsync(bool isDryRun, RunnerOptions options, IProgressTask task)
        {
            Solutions.IncrementForEach(task, 50, sln =>
            {
                sln.Changes.GroupBy(kvp => kvp.Value)
                           .ForEach(changes =>
                               CliRunner.ExecuteDotnet(isDryRun, new[]
                               {
                                   "sln",
                                   sln.Filepath,
                                   changes.Key == ChangeType.Added ? "add" : "remove",
                                   "--solution-folder",
                                   options.SolutionFolderName
                               }.Concat(changes.Select(change => change.Key.Filepath))
                                .ToArray()));
            });

            var projects = await Solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(projs => projs)
                                          .Distinct());

            projects.IncrementForEach(task, 50, proj => proj.WriteChanges());
        }

        record ReferenceNode(Project Project, ReferenceNode Parent);
    }
}
