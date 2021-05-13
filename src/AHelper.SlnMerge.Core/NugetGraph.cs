using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AHelper.SlnMerge.Core
{
    public class NugetGraph
    {
        private ProjectAssetsFile _assets;

        private NugetGraph(ProjectAssetsFile assets)
        {
            _assets = assets;
        }

        private static object BuildLock = new();

        public static NugetGraph Create(Project project, bool noRestore, IOutputWriter outputWriter)
        {

            if (!noRestore)
            {
                try
                {
                    lock (BuildLock)
                    {
                        using var projectCollection = new ProjectCollection();
                        var msbuildProject = new Microsoft.Build.Evaluation.Project(project.Filepath, new Dictionary<string, string>(), null, projectCollection, ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports);
                        var result = msbuildProject.Build("Restore");
                        if (!result) throw new Exception($"Failed to restore project '{project.Filepath}'");
                    }
                }
                catch (Exception ex)
                {
                    outputWriter.PrintWarning(ex);
                }
            }

            using (var projectCollection = new ProjectCollection())
            {
                var msbuildProject = new Microsoft.Build.Evaluation.Project(project.Filepath, new Dictionary<string, string>(), null, projectCollection, ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports);
                outputWriter.PrintProgress(msbuildProject.GetPropertyValue("ProjectAssetsFile"));
                if (string.IsNullOrEmpty(msbuildProject.GetPropertyValue("ProjectAssetsFile")))
                {
                    if (!noRestore)
                    {
                        outputWriter.PrintWarning(new Exception($"{project.Filepath} has no ProjectAssetsFile set, ignoring nugets"));
                    }
                    return new NugetGraph(null);
                }

                try
                {
                    var file = JsonSerializer.Deserialize<ProjectAssetsFile>(File.ReadAllText(msbuildProject.GetPropertyValue("ProjectAssetsFile")), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
                    return new NugetGraph(file);
                }
                catch (IOException)
                {
                    if (noRestore)
                    {
                        return new NugetGraph(null);
                    }
                    throw new FileReadException(FileReadExceptionType.ProjectAssetsJson, msbuildProject.GetPropertyValue("ProjectAssetsFile"), project.Filepath);
                }
            }
        }

        public IList<Reference> GetTransitiveReferencedProjects(Workspace workspace, IOutputWriter outputWriter)
        {
            if (_assets is null) return new List<Reference>();

            var projects = new Dictionary<Project, List<string>>();

            outputWriter.PrintTrace($"Scanning project.assets.json for {_assets}");
            foreach (var kvp in _assets.Targets.PackageModels)
            {
                outputWriter.PrintTrace($"- Checking framework '{kvp.Key}'");
                var packages = kvp.Value.Where(r => r.Type == "package").ToList();
                var transitives = kvp.Value.Where(r => packages.Any(p => p.Dependencies.ContainsKey(r.PackageId))).ToList();

                foreach (var package in packages)
                {
                    outputWriter.PrintTrace($"  - {_assets} has package {package.Name} with deps: {string.Join(", ", package.Dependencies.Keys)}");
                }
                foreach (var transitive in transitives)
                {
                    outputWriter.PrintTrace($"  - {_assets} has transitive {transitive.Name}");
                }

                transitives.Where(t => workspace.PackageLookup.ContainsKey(t.PackageId))
                           .Select(t => workspace.PackageLookup[t.PackageId])
                           .Distinct()
                           .ForEach(p =>
                           {
                               if (!projects.TryGetValue(p, out var frameworks))
                               {
                                   frameworks = new List<string>();
                                   projects[p] = frameworks;
                               }
                               frameworks.Add(kvp.Key);
                           });
            }

            return projects.Select(kvp => new Reference
                                          {
                                              Frameworks = kvp.Value,
                                              Include = kvp.Key.PackageId
                                          })
                           .ToList();
        }
    }

    public class ProjectAssetsFile
    {
        public int Version { get; set; }

        [JsonConverter(typeof(TargetsModelConverter))]
        public TargetsModel Targets { get; set; } = new();

        public ProjectModel Project { get; set; }

        public override string ToString()
            => Project.Restore.ProjectName;
    }

    public class TargetsModel
    {
        public Dictionary<string, List<TargetPackageModel>> PackageModels { get; set; } = new();
    }

    public class TargetPackageModel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Framework { get; set; }
        public string PackageId => Regex.Match(Name, "(.*)/.*").Groups[1].Value;
        public Dictionary<string, string> Dependencies { get; set; } = new();
        public Dictionary<string, object> Compile { get; set; } = new();
        public Dictionary<string, object> Runtime { get; set; } = new();

        public override string ToString()
            => Name;
    }

    public class ProjectModel
    {
        public ProjectRestoreModel Restore { get; set; }
        public Dictionary<string /* framework */, FrameworkModel> Frameworks { get; set; }

        public List<Reference> GetReferences()
        {
            var references = Frameworks.SelectMany(kvp => kvp.Value.Dependencies.Select(dep => new Reference
                                                                                               {
                                                                                                   Frameworks = new[] { kvp.Key },
                                                                                                   Include = dep.Key
                                                                                               }));
            return references.ToList();
        }

        public class FrameworkModel
        {
            public Dictionary<string /* packageId */, PackageModel> Dependencies { get; set; }
        }

        public class PackageModel
        {
            public string Target { get; set; }
            public string Version { get; set; }
        }
    }

    public class ProjectRestoreModel
    {
        public string ProjectName { get; set; }
        public string ProjectPath { get; set; }
    }

    public class TargetsModelConverter : JsonConverter<TargetsModel>
    {
        public override TargetsModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var targets = new TargetsModel();
            reader.Read();

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var targetName = reader.GetString();

                var match = Regex.Match(targetName, @".NETFramework,Version=v([\d.]+)");
                if (match.Success)
                {
                    targetName = $"net{match.Groups[1].Value.Replace(".", "")}";
                }

                match = Regex.Match(targetName, @".NETStandard,Version=v([\d.]+)");
                if (match.Success)
                {
                    targetName = $"netstandard{match.Groups[1].Value}";
                }

                match = Regex.Match(targetName, @".NETCoreApp,Version=v([\d.]+)");
                if (match.Success)
                {
                    targetName = $"netcoreapp{match.Groups[1].Value}";
                }

                reader.Read();
                reader.Read();

                var packages = new List<TargetPackageModel>();

                while(reader.TokenType == JsonTokenType.PropertyName)
                {
                    var packageName = reader.GetString();
                    reader.Read();
                    var packageModel = JsonSerializer.Deserialize<TargetPackageModel>(ref reader, options);
                    reader.Read();
                    packageModel.Name = packageName;
                    packages.Add(packageModel);
                }

                targets.PackageModels[targetName] = packages;
                reader.Read();
            }

            return targets;
        }

        public override void Write(Utf8JsonWriter writer, TargetsModel value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
