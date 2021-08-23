using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AHelper.SlnMerge.Core;
using Flurl.Http;
using Semver;

namespace AHelper.SlnMerge
{
    internal class Program
    {
        private static string MetadataPath => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "slnmerge", "metadata.json");

        static async Task Main(string[] args)
        {
            var outputWriter = new SpectreConsoleOutputWriter();
            await CheckForUpdatesAsync(outputWriter);
            await new Runner(outputWriter).RunAsync(args);
        }

        private static async Task CheckForUpdatesAsync(IOutputWriter outputWriter)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MetadataPath));
                if (File.Exists(MetadataPath))
                {
                    var metadata = JsonSerializer.Deserialize<Metadata>(await File.ReadAllTextAsync(MetadataPath));

                    if (DateTime.UtcNow - metadata.LastUpdateCheckTime >= Metadata.UpdateCheckInterval)
                    {
                        metadata.LastUpdateCheckTime = DateTime.UtcNow;
                        await File.WriteAllTextAsync(MetadataPath, JsonSerializer.Serialize(metadata));

                        var response = await "https://gitlab.com/api/v4/projects/24002513/releases".WithTimeout(TimeSpan.FromSeconds(5))
                                                                                                   .GetJsonListAsync();
                        var latestVersion = response.Select(release => release.name)
                                                    .Cast<string>()
                                                    .Select(name => name.TrimStart('v', 'V'))
                                                    .Select(name => SemVersion.Parse(name))
                                                    .OrderByDescending(version => version)
                                                    .FirstOrDefault();
                        var currentVersion = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location);

                        if (latestVersion != null && latestVersion != currentVersion.ProductVersion)
                        {
                            outputWriter.PrintInfo($"A new version of dotnet-slnmerge was released ({latestVersion}). To update: dotnet tool update -g dotnet-slnmerge");
                        }
                    }
                }
                else
                {
                    var metadata = new Metadata
                    {
                        LastUpdateCheckTime = DateTime.MinValue
                    };

                    await File.WriteAllTextAsync(MetadataPath, JsonSerializer.Serialize(metadata));
                }
            }
            catch
            {
                // Ignore all errors
            }
        }
    }
}
