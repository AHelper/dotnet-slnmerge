using System;
using System.Diagnostics;

namespace AHelper.SlnMerge.Core
{
    internal class CliRunner
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
