using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AHelper.SlnMerge
{
    internal class Solution
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
}
