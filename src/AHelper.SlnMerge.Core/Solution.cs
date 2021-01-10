using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AHelper.SlnMerge.Core
{
    public class Solution
    {
        public AsyncLazy<List<Project>> Projects { get; private set; }
        public IDictionary<Project, ChangeType> Changes { get; } = new ConcurrentDictionary<Project, ChangeType>();
        public string Filepath { get; }

        private readonly IOutputWriter _outputWriter;

        public Solution(IOutputWriter outputWriter, string filepath)
        {
            if (Directory.Exists(filepath))
            {
                var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(filepath));
                filepath = Path.Join(filepath, $"{directoryName}.sln");
            }

            // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            // {
            //     filepath = filepath.ToLowerInvariant();
            // }

            if (!File.Exists(filepath))
            {
                throw new FileReadException(FileReadExceptionType.Sln, Path.GetFullPath(filepath), null);
            }

            Filepath = Path.GetFullPath(filepath);
            Projects = new AsyncLazy<List<Project>>(GetProjectsAsync);
            _outputWriter = outputWriter;
        }

        public async Task CheckDetachedProjectReferences()
        {
            var projs = await Projects.Value;
            var filepaths = projs.Select(proj => proj.Filepath).ToList();

            foreach (var proj in projs)
            {
                foreach (var projectRef in proj.ProjectReferences)
                {
                    var refPath = Path.GetFullPath(projectRef, Path.GetDirectoryName(proj.Filepath));

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

        internal async Task ReplaceProjects(IEnumerable<Project> projects)
        {
            var myProjects = await Projects.Value;

            Projects = new AsyncLazy<List<Project>>(() => myProjects.Join(projects, proj => proj.Filepath, proj => proj.Filepath, (_, p) => p)
                                                                    .ToList());
        }

        private IList<string> GetProjectPaths()
            => CliRunner.ExecuteDotnet(false, "sln", Filepath, "list")
                            .Split('\n', '\r')
                            .Where(path => !string.IsNullOrWhiteSpace(path))
                            .Skip(2) // `dotnet sln list` includes 2-line header
                            .ToList();
        private Task<List<Project>> GetProjectsAsync()
            => GetProjectPaths().Select(path => Project.CreateAsync(path, this, _outputWriter))
                                .WhenAll()
                                .ToListAsync();
    }
}
