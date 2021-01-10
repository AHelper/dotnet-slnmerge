using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;

namespace AHelper.SlnMerge.Core
{
    public class Project
    {
        public string PackageId { get; }
        public IList<string> PackageReferences { get; }
        public IList<string> ProjectReferences { get; }
        public string Filepath { get; }
        public Solution Parent { get; }
        public IDictionary<Project, ChangeType> Changes { get; } = new ConcurrentDictionary<Project, ChangeType>();

        private IOutputWriter _outputWriter;

        private Project(string filepath,
                        string packageId,
                        IList<string> packageReferences,
                        IList<string> projectReferences,
                        IOutputWriter outputWriter,
                        Solution parent)
        {
            Filepath = filepath;
            PackageId = packageId;
            PackageReferences = packageReferences;
            ProjectReferences = projectReferences;
            _outputWriter = outputWriter;
            Parent = parent;
        }

        public static async Task<Project> CreateAsync(string filepath, Solution parent, IOutputWriter outputWriter)
        {
            filepath = Path.GetFullPath(filepath, Path.GetDirectoryName(parent.Filepath));

            if (!File.Exists(filepath))
                throw new FileReadException("Project does not exist")
                {
                    FilePath = filepath,
                    FileType = FileReadExceptionType.Csproj,
                    ReferencedBy = parent.Filepath
                };

            using var projectCollection = new ProjectCollection();
            var msbuildProject = new Microsoft.Build.Evaluation.Project(filepath, new Dictionary<string, string>(), null, projectCollection);

            var packageId = await GetPackageId(filepath, msbuildProject);
            var packageReferences = GetItems(msbuildProject, "PackageReference");
            var projectReferences = GetItems(msbuildProject, "ProjectReference");

            // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            // {
            //     filepath = filepath.ToLowerInvariant();
            //     projectReferences = projectReferences.Select(path => path.ToLowerInvariant())
            //                                          .ToList();
            // }

            return new Project(filepath, packageId, packageReferences, projectReferences, outputWriter, parent);
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

            var nuspecFile = msbuildProject.GetPropertyValue("NuspecFile");
            var nuspecPath = Path.Combine(Path.GetDirectoryName(filepath), nuspecFile);

            if (!string.IsNullOrEmpty(nuspecFile) && !File.Exists(nuspecPath))
            {
                throw new FileReadException("Nuspec file could not be found")
                {
                    FilePath = nuspecPath,
                    FileType = FileReadExceptionType.Nuspec,
                    ReferencedBy = filepath
                };
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
}
