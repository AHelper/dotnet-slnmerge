using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace AHelper.SlnMerge.Core
{
    public class Reference
    {
        public IList<string> Frameworks { get; set; } = new List<string>();
        public string Include { get; set; }
        public string Origin { get; set; }
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
        public bool IsLegacy { get; }

        private IOutputWriter _outputWriter;

        private Project(string filepath,
                        string packageId,
                        IList<Reference> packageReferences,
                        IList<Reference> projectReferences,
                        IList<string> targetFrameworks,
                        IOutputWriter outputWriter,
                        Solution parent,
                        bool isLegacy)
        {
            Filepath = filepath;
            PackageId = packageId;
            PackageReferences = packageReferences;
            ProjectReferences = projectReferences;
            TargetFrameworks = targetFrameworks;
            _outputWriter = outputWriter;
            Parent = parent;
            IsLegacy = isLegacy;
        }

        public static async Task<Project> CreateAsync(string filepath, Solution parent, IOutputWriter outputWriter)
        {
            filepath = Path.GetFullPath(filepath, Path.GetDirectoryName(parent.Filepath));

            if (!File.Exists(filepath))
                throw new FileReadException(FileReadExceptionType.Csproj, filepath, parent.Filepath);

            using var projectCollection = new ProjectCollection();
            var msbuildProject = new Microsoft.Build.Evaluation.Project(filepath, new Dictionary<string, string>(), null, projectCollection, ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports);
            var usingSdk = msbuildProject.Properties.FirstOrDefault(prop => prop.Name == "UsingMicrosoftNETSdk")?.EvaluatedValue.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false;
            var targetFrameworks = Array.Empty<string>();

            if (usingSdk)
            {
                targetFrameworks = msbuildProject.GetProperty("TargetFrameworks") switch
                {
                    ProjectProperty pp => pp.EvaluatedValue.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(val => val.Trim()).ToArray(),
                    null => new[] { msbuildProject.GetPropertyValue("TargetFramework") }
                };
            }
            else
            {
                targetFrameworks = new[] {
                    msbuildProject.GetPropertyValue("TargetFrameworkVersion")
                                  .Replace("v", "net")
                                  .Replace(".", "")
                };
            }

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

                foreach (var item in msbuildProject.GetItems("ProjectReference"))
                {
                    var include = ConvertPathSeparators(item.EvaluatedInclude);

                    if (!projectReferences.TryGetValue(include, out var reference))
                    {
                        reference = new Reference
                        {
                            Include = include,
                            Origin = item.GetMetadataValue("Origin")
                        };
                        projectReferences.Add(include, reference);
                    }

                    reference.Frameworks.Add(targetFramework);
                }
            }

            var packageId = await GetPackageId(filepath, msbuildProject);

            return new Project(filepath, packageId, packageReferences.Values.ToList(), projectReferences.Values.ToList(), targetFrameworks, outputWriter, parent, !usingSdk);
        }

        internal static Project CreateForTesting(string filepath,
                                                 string packageId)
            => new(filepath, packageId, Array.Empty<Reference>(), Array.Empty<Reference>(), Array.Empty<string>(), null, null, false);

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

        public void AddTransitiveReferences(Workspace workspace, RunnerOptions options)
        {
            var graph = NugetGraph.Create(this, options.NoRestore, _outputWriter);

            foreach (var newReference in graph.GetTransitiveReferencedProjects(workspace, IsLegacy, _outputWriter))
            {
                foreach (var framework in newReference.Frameworks)
                {
                    var projectReferences = GetProjectReferences(workspace, new[] { framework });
                    if (projectReferences.Any(r => r.PackageId == newReference.Include))
                    {
                        continue;
                    }

                    Changes.Add(new Change
                    {
                        Project = workspace.PackageLookup[newReference.Include],
                        Framework = framework,
                        ChangeType = ChangeType.Added
                    });
                }
            }
        }

        public void RemoveReferences(Workspace workspace)
            => ProjectReferences.Where(reference => reference.Origin == "slnmerge")
                                .SelectMany(reference => reference.Frameworks.Select(framework => (reference, framework)))
                                .ForEach(tuple => Changes.Add(new Change
                                                              {
                                                                  ChangeType = ChangeType.Removed,
                                                                  Framework = tuple.framework,
                                                                  Project = workspace.PackageLookup.Values.First(p => p.Filepath == Path.GetFullPath(tuple.reference.Include, Path.GetDirectoryName(Filepath)))
                                                              }));

        public void WriteChanges()
        {
            using var projectCollection = new ProjectCollection();
            var project = ProjectRootElement.Open(Filepath, projectCollection, true);
            var changes = Changes.Where(change => change.ChangeType == ChangeType.Added || change.ChangeType == ChangeType.Removed)
                                 .GroupBy(change => change.Project)
                                 .Select(change => TargetFrameworks.All(tf => change.Any(c => c.Framework == tf))
                                     ? (change.Key, frameworks: new[] { "" }) 
                                     : (change.Key, frameworks: change.Select(c => c.Framework)))
                                 .SelectMany(tuple => tuple.frameworks.Select(framework => (tuple.Key, framework)));
            var changeTypeDict = Changes.Where(change => change.ChangeType == ChangeType.Added || change.ChangeType == ChangeType.Removed)
                                        .DistinctBy(change => change.Project)
                                        .ToDictionary(c => c.Project, c => c.ChangeType);

            foreach (var change in changes.Where(c => changeTypeDict.TryGetValue(c.Key, out var ct) && ct == ChangeType.Added))
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

            foreach (var change in changes.Where(c => changeTypeDict[c.Key] == ChangeType.Removed))
            {
                var item = project.ItemGroups
                                  .SelectMany(ig => ig.Items)
                                  .FirstOrDefault(item => item.ElementName == "ProjectReference"
                                                          && Path.GetFullPath(ConvertPathSeparators(item.Include), Path.GetDirectoryName(Filepath)) == change.Key.Filepath);

                if (item == null)
                {
                    _outputWriter.PrintWarning(new Exception($"Could not find ProjectReference in {Filepath} to remove for {change.Key.Filepath}"));
                    continue;
                }

                var itemGroup = item.Parent as ProjectItemGroupElement;
                itemGroup.RemoveChild(item);

                if (!itemGroup.Children.Any())
                {
                    itemGroup.Parent.RemoveChild(itemGroup);
                }
            }

            if (Changes.Any(change => change.ChangeType == ChangeType.AddedVersion))
            {
                var versions = project.Properties.Where(prop => prop.Name == "Version").ToList();
                var needsGenerated = true;

                foreach (var version in versions)
                {
                    if (version.Condition?.Contains("slnmerge") ?? false)
                    {
                        if (version.Condition.Contains("Generated"))
                        {
                            needsGenerated = false;
                        }
                        continue;
                    }

                    if (string.IsNullOrEmpty(version.Condition))
                    {
                        version.Condition = "'Original'=='slnmerge'";
                    }
                    else
                    {
                        version.Condition = $"'Original'=='slnmerge'&&{version.Condition}";
                    }
                }

                if (needsGenerated)
                {
                    var generatedVersion = project.AddProperty("Version", "9999.0.0");
                    generatedVersion.Condition = "'Generated'!='slnmerge'";
                }
            }

            if (Changes.Any(change => change.ChangeType == ChangeType.RemovedVersion))
            {
                var versions = project.Properties.Where(prop => prop.Name == "Version").ToList();

                foreach (var version in versions)
                {
                    if (version.Condition?.Contains("slnmerge") ?? false)
                    {
                        if (version.Condition.Contains("'Generated'!='slnmerge'"))
                        {
                            version.Parent.RemoveChild(version);
                            continue;
                        }
                        if (version.Condition.Contains("'Original'=='slnmerge'"))
                        {
                            version.Condition = Regex.Replace(version.Condition, "'Original'=='slnmerge'(&&)?", "");
                        }
                    }
                }
            }

            project.Save();

            bool IsConditionForFramework(string condition, string framework)
                => framework == string.Empty && condition == string.Empty ||
                   Regex.IsMatch(condition, $"'\\$\\(TargetFramework\\)'\\s*==\\s*'{framework}'");

            bool DoesItemGroupContainProjectReference(ProjectItemGroupElement itemGroup)
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

        public async Task AddVersionOverrideAsync(Workspace workspace)
        {
            var projects = await workspace.Solutions.Select(sln => sln.Projects.Value).WhenAll(projs => projs.SelectMany(projs => projs)).ToListAsync();
            var isReferenced = projects.Any(proj => proj.PackageReferences.Select(reference => (reference, path: Path.GetFullPath(reference.Include, Path.GetDirectoryName(Filepath))))
                                                                          .Join(workspace.PackageLookup.Values, tuple => tuple.path, proj => proj.Filepath, (tuple, proj) => (tuple.reference, proj))
                                                                          .Select(tuple => tuple.proj)
                                                                          .Concat(proj.Changes.Where(change => change.ChangeType == ChangeType.Added)
                                                                                              .Select(kvp => kvp.Project))
                                                                          .Any(p => p == this));

            if (isReferenced)
            {
                using var projectCollection = new ProjectCollection();
                var msbuildProject = ProjectRootElement.Open(Filepath, projectCollection, true);
                var versions = msbuildProject.Properties.Where(prop => prop.Name == "Version").ToList();

                if (!versions.Any(version => version.Condition?.Contains("slnmerge") ?? false))
                {
                    Changes.Add(new Change
                    {
                        ChangeType = ChangeType.AddedVersion
                    });
                }
            }
        }

        public Task RemoveVersionOverrideAsync()
        {
            Changes.Add(new Change
            {
                ChangeType = ChangeType.RemovedVersion
            });
            return Task.CompletedTask;
        }

        public IEnumerable<Project> GetProjectReferences(Workspace workspace, ICollection<string> frameworks = null)
            => ProjectReferences.Where(reference => frameworks == null || reference.Frameworks.Intersect(frameworks).Any())
                                .Select(reference => (reference, path: Path.GetFullPath(reference.Include, Path.GetDirectoryName(Filepath))))
                                .Join(workspace.PackageLookup.Values, tuple => tuple.path, proj => proj.Filepath, (tuple, proj) => (tuple.reference, proj))
                                .Where(tuple => tuple.reference.Frameworks.Any(fw => !Changes.Any(c => c.ChangeType == ChangeType.Removed && c.Project == tuple.proj && fw == c.Framework)))
                                .Select(tuple => tuple.proj)
                                .Concat(Changes.Where(change => change.ChangeType == ChangeType.Added)
                                               .Where(change => frameworks == null || frameworks.Contains(change.Framework))
                                               .Select(kvp => kvp.Project))
                                .Distinct();

        public override string ToString()
            => $"Project({Filepath})";

        private static string ConvertPathSeparators(string path)
            => Regex.Replace(path, "[/\\\\]", Path.DirectorySeparatorChar.ToString());
    }
}
