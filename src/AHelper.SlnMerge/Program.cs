using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace AHelper.SlnMerge
{
    enum ChangeType
    {
        Added,
        Removed
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = await Workspace.CreateAsync(args);

            await workspace.AddReferencesAsync();
            await workspace.PopulateSolutionsAsync();
            await workspace.CommitChangesAsync(false);
        }
    }

    class AsyncLazy<T> : Lazy<Task<T>>
    {
        public AsyncLazy(Func<T> factory)
            : base(() => Task.Factory.StartNew(factory))
        { }

        public AsyncLazy(Func<Task<T>> factory)
            : base(() => Task.Factory.StartNew(factory).Unwrap())
        { }

        public TaskAwaiter<T> GetAwaiter()
            => Value.GetAwaiter();
    }

    class Project
    {
        public string PackageId { get; }
        public IList<string> PackageReferences { get; }
        public IList<string> ProjectReferences { get; }
        public string Filepath { get; }
        public Solution Parent { get; }
        public IDictionary<Project, ChangeType> Changes { get; } = new ConcurrentDictionary<Project, ChangeType>();

        private Project(string filepath,
                        string packageId,
                        IList<string> packageReferences,
                        IList<string> projectReferences,
                        Solution parent)
        {
            Filepath = filepath;
            PackageId = packageId;
            PackageReferences = packageReferences;
            ProjectReferences = projectReferences;
            Parent = parent;
        }

        public static async Task<Project> CreateAsync(string filepath, Solution parent)
        {
            filepath = Path.GetFullPath(filepath, Path.GetDirectoryName(parent.Filepath));

            if (!File.Exists(filepath))
                throw new FileNotFoundException(filepath);

            using var projectCollection = new ProjectCollection();
            var msbuildProject = new Microsoft.Build.Evaluation.Project(filepath, new Dictionary<string, string>(), null, projectCollection);

            var packageId = await GetPackageId(filepath, msbuildProject);
            var packageReferences = GetItems(msbuildProject, "PackageReference");
            var projectReferences = GetItems(msbuildProject, "ProjectReference");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filepath = filepath.ToLowerInvariant();
                projectReferences = projectReferences.Select(path => path.ToLowerInvariant())
                                                     .ToList();
            }

            return new Project(filepath, packageId, packageReferences, projectReferences, parent);
        }

        public IList<string> GetUnresolvedPackageReferences(Workspace workspace)
            => PackageReferences.Except(ProjectReferences.Join(workspace.PackageLookup.Values,
                                                               path => Path.GetFullPath(path, Path.GetDirectoryName(Filepath)),
                                                               proj => proj.Filepath,
                                                               (path, proj) => proj.PackageId))
                                .ToList();

        public void AddReferences(Workspace workspace)
            => GetUnresolvedPackageReferences(workspace).Select(workspace.PackageLookup.GetValueOrDefault)
                                                        .Where(proj => proj != null)
                                                        .ForEach(proj => Changes[proj] = ChangeType.Added);

        private static IList<string> GetItems(Microsoft.Build.Evaluation.Project msbuildProject, string itemType)
            => msbuildProject.Items.Where(item => item.ItemType == itemType)
                                   .Select(item => item.EvaluatedInclude)
                                   .ToList();

        private static async Task<string> GetPackageId(string filepath, Microsoft.Build.Evaluation.Project msbuildProject)
        {
            try
            {
                var nuspec = await XDocument.LoadAsync(File.OpenRead(Path.ChangeExtension(filepath, "nuspec")), LoadOptions.None, default);
                var namespaceManager = new XmlNamespaceManager(nuspec.CreateNavigator().NameTable);
                namespaceManager.AddNamespace("nuspec", "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd");

                var idElement = nuspec.Root.XPathSelectElement("/nuspec:package/nuspec:metadata/nuspec:id", namespaceManager);

                if (idElement != null)
                {
                    return idElement.Value;
                }
            }
            catch (FileNotFoundException)
            {
                // ignore
            }

            var packageId = msbuildProject.GetPropertyValue("PackageId");

            if (!string.IsNullOrEmpty(packageId))
            {
                return packageId;
            }

            return Path.GetFileNameWithoutExtension(filepath);
        }

        public async Task PopulateSolutionAsync(Solution parent, Workspace workspace)
        {
            Console.WriteLine($"Checking {Path.GetFileName(parent.Filepath)} for {PackageId}");
            if (!(await parent.Projects).Contains(this))
            {
                parent.Changes[this] = ChangeType.Added;
            }

            await ProjectReferences.Select(path => Path.GetFullPath(path, Path.GetDirectoryName(Filepath)))
                                   .Join(workspace.PackageLookup.Values, path => path, proj => proj.Filepath, (path, proj) => proj)
                                   .Select(proj => proj.PopulateSolutionAsync(parent, workspace))
                                   .WhenAll();
        }

        public IEnumerable<Project> GetProjectReferences(Workspace workspace)
            => ProjectReferences.Select(path => Path.GetFullPath(path, Path.GetDirectoryName(Filepath)))
                                .Join(workspace.PackageLookup.Values, path => path, proj => proj.Filepath, (path, proj) => proj)
                                .Concat(Changes.Where(kvp => kvp.Value == ChangeType.Added).Select(kvp => kvp.Key));
    }

    class Solution
    {
        public AsyncLazy<List<Project>> Projects { get; }
        public IDictionary<Project, ChangeType> Changes { get; } = new ConcurrentDictionary<Project, ChangeType>();
        public string Filepath { get; }

        public Solution(string filepath)
        {
            if (Directory.Exists(filepath))
            {
                var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(filepath));
                filepath = Path.Join(filepath, $"{directoryName}.sln");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filepath = filepath.ToLowerInvariant();
            }

            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException(filepath);
            }

            Filepath = Path.GetFullPath(filepath);
            Projects = new AsyncLazy<List<Project>>(GetProjectsAsync);
        }

        private IList<string> GetProjectPaths()
            => CliRunner.ExecuteDotnet(false, "sln", Filepath, "list")
                            .Split('\n', '\r')
                            .Where(path => !string.IsNullOrWhiteSpace(path))
                            .Skip(2) // `dotnet sln list` includes 2-line header
                            .ToList();
        private Task<List<Project>> GetProjectsAsync()
            => GetProjectPaths().Select(path => Project.CreateAsync(path, this))
                                .WhenAll()
                                .ToListAsync();
    }

    class Workspace
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

    static class EnumerableExtensions
    {
        public static Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> enumerable)
            => Task.WhenAll(enumerable).ContinueWith(task => task.Result.AsEnumerable());

        public static Task WhenAll(this IEnumerable<Task> enumerable)
            => Task.WhenAll(enumerable);

        public static Task WhenAll(this Task<IEnumerable<Task>> enumerable)
            => enumerable.ContinueWith(value => Task.WhenAll(value));

        public static Task WhenAll<T>(this IEnumerable<Task<T>> enumerable, Action<IEnumerable<T>> predicate)
            => Task.WhenAll(enumerable).ContinueWith(task => predicate(task.Result));

        public static Task<IEnumerable<TOut>> WhenAll<TIn, TOut>(this IEnumerable<Task<TIn>> enumerable, Func<IEnumerable<TIn>, IEnumerable<TOut>> predicate)
            => Task.WhenAll(enumerable).ContinueWith(task => predicate(task.Result));

        public static Task<List<T>> ToListAsync<T>(this Task<IEnumerable<T>> enumerable)
            => enumerable.ContinueWith(task => task.Result.ToList());

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> predicate)
        {
            foreach (var element in enumerable)
            {
                predicate(element);
            }
        }
    }

    class CliRunner
    {
        public static string ExecuteDotnet(bool isDryRun, params string[] arguments)
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            foreach (var arg in arguments)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            var command = $"{process.StartInfo.FileName} {string.Join(' ', process.StartInfo.ArgumentList)}";

            if (isDryRun)
            {
                Console.WriteLine(command);
                return "";
            }
            else
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Command '{command}' existed with code {process.ExitCode}:\n{error}");
                }

                return output;
            }
        }
    }
}
