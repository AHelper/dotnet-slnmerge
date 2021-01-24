using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

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

        public static async Task<Workspace> CreateAsync(IOutputWriter outputWriter, IEnumerable<string> solutionPaths)
        {
            var solutions = solutionPaths.Select(path => new Solution(outputWriter, path))
                                         .ToList();
            await Task.WhenAll(solutions.Select(sln => sln.Projects.Value));
            await DeduplicateProjects(solutions);
            await Task.WhenAll(solutions.Select(sln => sln.CheckDetachedProjectReferences()));
            return new Workspace(outputWriter, solutions);
        }

        public static async Task DeduplicateProjects(List<Solution> solutions)
        {
            var projects = await solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(projs => projs)
                                                                 .DistinctBy(proj => proj.Filepath));
            
            foreach(var sln in solutions)
            {
                await sln.ReplaceProjects(projects);
            }
        }

        public async Task AddReferencesAsync()
            => await Solutions.Select(sln => sln.Projects.Value)
                              .WhenAll(projs => projs.SelectMany(projs => projs)
                                                     .ForEach(proj => proj.AddReferences(this)));

        public async Task RemoveReferencesAsync()
            => await Solutions.Select(sln => sln.Projects.Value)
                              .WhenAll(projs => projs.SelectMany(projs => projs)
                                                     .ForEach(proj => proj.RemoveReferences(this)));

        public async Task CheckForCircularReferences()
        {
            var projects = await Solutions.Select(sln => sln.Projects.Value)
                                          .WhenAll(projs => projs.SelectMany(projs => projs)
                                                                 .Distinct())
                                          .ToDictionaryAsync(proj => proj.PackageId,
                                                             proj => proj);

            foreach (var project in projects.Values)
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
            }
        }

        public async Task PopulateSolutionsAsync()
        {
            foreach (var sln in Solutions)
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
            }
        }

        public Task CleanupSolutionsAsync()
            => Solutions.Select(sln => sln.PruneProjectsAsync(this))
                        .WhenAll();

        public async Task CommitChangesAsync(bool isDryRun)
        {
            foreach (var sln in Solutions)
            {
                sln.Changes.GroupBy(kvp => kvp.Value)
                           .ForEach(changes =>
                               CliRunner.ExecuteDotnet(isDryRun, new[]
                               {
                                   "sln",
                                   sln.Filepath,
                                   changes.Key == ChangeType.Added ? "add" : "remove"
                               }.Concat(changes.Select(change => change.Key.Filepath))
                                .ToArray()));

                foreach (var project in await sln.Projects)
                {
                    project.WriteChanges();
                }
            }
        }

        record ReferenceNode(Project Project, ReferenceNode Parent);
    }
}
