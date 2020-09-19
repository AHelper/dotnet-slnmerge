using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AHelper.SlnMerge
{
    internal class Workspace
    {
        public List<Solution> Solutions { get; }
        public Dictionary<string /* PackageId */, Project> PackageLookup { get; }

        private Workspace(List<Solution> solutions)
        {
            Solutions = solutions;

            try
            {
                PackageLookup = solutions.SelectMany(sln => sln.Projects.Value.Result)
                                         .GroupBy(proj => proj.Filepath, (path, projs) => projs.First())
                                         .ToDictionary(proj => proj.PackageId, proj => proj);
            }
            catch (ArgumentException)
            {
                var error = new StringBuilder();
                error.AppendLine("Multiple projects have the same PackageId:");

                solutions.SelectMany(sln => sln.Projects.Value.Result)
                         .GroupBy(proj => proj.PackageId)
                         .Where(projs => projs.Count() > 1)
                         .SelectMany(projs => projs)
                         .ForEach(proj => error.AppendLine($"{proj.PackageId} => {proj.Filepath}"));

                throw new Exception(error.ToString());
            }
        }

        public static async Task<Workspace> CreateAsync(IEnumerable<string> solutionPaths)
        {
            var solutions = solutionPaths.Select(path => new Solution(path))
                                         .ToList();
            await Task.WhenAll(solutions.Select(sln => sln.Projects.Value));
            return new Workspace(solutions);
        }

        public async Task AddReferencesAsync()
            => await Solutions.Select(sln => sln.Projects.Value)
                             .WhenAll(projs => projs.SelectMany(projs => projs)
                                                    .ForEach(proj => proj.AddReferences(this)));

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
                    project.Changes.GroupBy(kvp => kvp.Value)
                                   .ForEach(changes =>
                                       CliRunner.ExecuteDotnet(isDryRun, new[]
                                       {
                                           changes.Key == ChangeType.Added ? "add" : "remove",
                                           project.Filepath,
                                           "reference"
                                       }.Concat(changes.Select(change => change.Key.Filepath))
                                        .ToArray()));
                }
            }
        }
    }
}
