using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Evaluation;

namespace AHelper.SlnMerge.Core
{
    public class Reference
    {
        public IList<string> Frameworks { get; set; } = new List<string>();
        public string Include { get; set; }
    }

    public class Change
    {
        public ChangeType ChangeType { get; set; }
        public string Framework { get; set; }
        public Project Project { get; set; }
    }

    public class Project
    {
        public string PackageId { get; }
        public IList<Reference> PackageReferences { get; }
        public IList<Reference> ProjectReferences { get; }
        public IList<string> TargetFrameworks { get; set; }
        public string Filepath { get; }
        public Solution Parent { get; }
        public IList<Change> Changes { get; } = new List<Change>();

        private IOutputWriter _outputWriter;

        private Project(string filepath,
                        string packageId,
                        IList<Reference> packageReferences,
                        IList<Reference> projectReferences,
                        IList<string> targetFrameworks,
                        IOutputWriter outputWriter,
                        Solution parent)
        {
            Filepath = filepath;
            PackageId = packageId;
            PackageReferences = packageReferences;
            ProjectReferences = projectReferences;
            TargetFrameworks = targetFrameworks;
            _outputWriter = outputWriter;
            Parent = parent;
        }

        public static async Task<Project> CreateAsync(string filepath, Solution parent, IOutputWriter outputWriter)
        {
            filepath = Path.GetFullPath(filepath, Path.GetDirectoryName(parent.Filepath));

            if (!File.Exists(filepath))
                throw new FileReadException(FileReadExceptionType.Csproj, filepath, parent.Filepath);

            using var projectCollection = new ProjectCollection();
            var msbuildProject = new Microsoft.Build.Evaluation.Project(filepath, new Dictionary<string, string>(), null, projectCollection);
            var targetFrameworks = msbuildProject.GetProperty("TargetFrameworks") switch
            {
                ProjectProperty pp => pp.EvaluatedValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                null => new[] { msbuildProject.GetPropertyValue("TargetFramework") }
            };

            var packageReferences = new Dictionary<string, Reference>();
            var projectReferences = new Dictionary<string, Reference>();

            foreach(var targetFramework in targetFrameworks)
            {
                msbuildProject.SetGlobalProperty("TargetFramework", targetFramework);
                msbuildProject.ReevaluateIfNecessary();

                foreach (var include in GetItems(msbuildProject, "PackageReference"))
                {
                    if (!packageReferences.TryGetValue(include, out var reference))
                    {
                        reference = new Reference
                        {
                            Include = include
                        };
                        packageReferences.Add(include, reference);
                    }

                    reference.Frameworks.Add(targetFramework);
                }

                foreach (var include in GetItems(msbuildProject, "ProjectReference"))
                {
                    if (!projectReferences.TryGetValue(include, out var reference))
                    {
                        reference = new Reference
                        {
                            Include = include
                        };
                        projectReferences.Add(include, reference);
                    }

                    reference.Frameworks.Add(targetFramework);
                }
            }

            var packageId = await GetPackageId(filepath, msbuildProject);

            return new Project(filepath, packageId, packageReferences.Values.ToList(), projectReferences.Values.ToList(), targetFrameworks, outputWriter, parent);
        }

        internal static Project CreateForTesting(string filepath,
                                                 string packageId)
            => new(filepath, packageId, Array.Empty<Reference>(), Array.Empty<Reference>(), Array.Empty<string>(), null, null);

        public IList<Reference> GetUnresolvedPackageReferences(Workspace workspace)
        {
            var projectReferences = ProjectReferences.Join(workspace.PackageLookup.Values,
                                                           reference => Path.GetFullPath(reference.Include, Path.GetDirectoryName(Filepath)),
                                                           proj => proj.Filepath,
                                                           (reference, proj) => new KeyValuePair<string, Reference>(proj.PackageId, reference))
                                                     .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return PackageReferences.Select(reference =>
                {
                    if (projectReferences.TryGetValue(reference.Include, out var projectReference) &&
                        projectReference.Frameworks.OrderBy(f => f).SequenceEqual(reference.Frameworks.OrderBy(f => f)))
                    {
                        return null;
                    }

                    return new Reference
                    {
                        Include = reference.Include,
                        Frameworks = projectReference is not null
                            ? reference.Frameworks.Except(reference.Frameworks.Intersect(projectReference.Frameworks)).ToList()
                            : reference.Frameworks
                    };
                })
                .Where(reference => reference is not null)
                .ToList();
        }

        public void AddReferences(Workspace workspace)
            => GetUnresolvedPackageReferences(workspace).Where(reference => workspace.PackageLookup.ContainsKey(reference.Include))
                                                        .ForEach(reference => reference.Frameworks.ForEach(framework =>
                                                            Changes.Add(new Change 
                                                                        { 
                                                                            ChangeType = ChangeType.Added,
                                                                            Framework = framework, 
                                                                            Project = workspace.PackageLookup[reference.Include] 
                                                                        })));

        public void WriteChanges()
        {
            var project = Microsoft.Build.Construction.ProjectRootElement.Open(Filepath, new ProjectCollection(), true);
            var changes = Changes.GroupBy(change => change.Project)
                                 .Select(change => TargetFrameworks.All(tf => change.Any(c => c.Framework == tf))
                                     ? (change.Key, frameworks: new[] { "" }) 
                                     : (change.Key, frameworks: change.Select(c => c.Framework)))
                                 .SelectMany(tuple => tuple.frameworks.Select(framework => (tuple.Key, framework)));

            foreach (var change in changes)
            {
                var itemGroup = project.ItemGroups.FirstOrDefault(ig => IsConditionForFramework(ig.Condition, change.framework) && DoesItemGroupContainProjectReference(ig));

                if (itemGroup == null)
                {
                    itemGroup = project.AddItemGroup();

                    if (change.framework != string.Empty)
                    {
                        itemGroup.Condition = $"'$(TargetFramework)' == '{change.framework}'";
                    }
                }

                var item = itemGroup.AddItem("ProjectReference", Path.GetRelativePath(Path.GetDirectoryName(Filepath), change.Key.Filepath));
                item.AddMetadata("Origin", "slnmerge", true);
            }

            project.Save();

            bool IsConditionForFramework(string condition, string framework)
                => framework == string.Empty && condition == string.Empty ||
                   Regex.IsMatch(condition, $"'$(TargetFramework)'\\s*==\\s*'{framework}'");

            bool DoesItemGroupContainProjectReference(Microsoft.Build.Construction.ProjectItemGroupElement itemGroup)
                => itemGroup.Children.Any(item => item.ElementName == "ProjectReference");
        }

        private static IList<string> GetItems(Microsoft.Build.Evaluation.Project msbuildProject, string itemType)
            => msbuildProject.GetItems(itemType)
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

            var nuspecFile = msbuildProject.GetPropertyValue("NuspecFile");
            var nuspecPath = Path.Combine(Path.GetDirectoryName(filepath), nuspecFile);

            if (!string.IsNullOrEmpty(nuspecFile) && !File.Exists(nuspecPath))
            {
                throw new FileReadException(FileReadExceptionType.Nuspec, nuspecPath, filepath);
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

            await ProjectReferences.Select(reference => Path.GetFullPath(reference.Include, Path.GetDirectoryName(Filepath)))
                                   .Join(workspace.PackageLookup.Values, path => path, proj => proj.Filepath, (path, proj) => proj)
                                   .Select(proj => proj.PopulateSolutionAsync(parent, workspace))
                                   .WhenAll();
        }

        public IEnumerable<Project> GetProjectReferences(Workspace workspace)
            => ProjectReferences.Select(reference => Path.GetFullPath(reference.Include, Path.GetDirectoryName(Filepath)))
                                .Join(workspace.PackageLookup.Values, path => path, proj => proj.Filepath, (path, proj) => proj)
                                .Concat(Changes.Select(kvp => kvp.Project));
    }
}
