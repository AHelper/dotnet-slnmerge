using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Build.Locator;
using CommandLine.Text;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Diagnostics;
using System.Runtime.Loader;
using System.IO;
using Spectre.Console;

namespace AHelper.SlnMerge.Core
{
    public class RunnerOptions
    {
        [Value(0, MetaName = "solutions", HelpText = "List of solutions", Required = true)]
        public IList<string> Solutions { get; set; }

        [Option('p', "property", HelpText = "MSBuild property (key=value) when loading projects")]
        public IEnumerable<string> PropertyStrings { get; set; }

        [Option('v', "verbosity", HelpText = "Set verbosity (verbose|info|warning|error|off)", Default = TraceLevel.Info)]
        public TraceLevel Verbosity { get; set; }

        [Option('u', "undo", HelpText = "Removes ProjectReferences that this tool has added", Default = false)]
        public bool Undo { get; set; }

        [Option("no-restore", HelpText = "Disables restoring nugets while scanning. May not add projects with transitive references", Default = false)]
        public bool NoRestore { get; set; }

        public IDictionary<string, string> Properties => PropertyStrings.Select(str => str.Split('=', 2))
                                                                        .Where(pair => pair.Length == 2)
                                                                        .ToDictionary(pair => pair[0], pair => pair[1]);
    }

    public class Runner
    {
        private readonly IOutputWriter _outputWriter;

        static Runner()
        {
            var instance = MSBuildLocator.RegisterDefaults(); 
            // Load NuGet DLLs from MSBuild directory (not handled by MSBuildLocator)
            AssemblyLoadContext.Default.Resolving += (assemblyLoadContext, assemblyName) =>
            {
                var fullpath = Path.Combine(instance.MSBuildPath, $"{assemblyName.Name}.dll");
                return File.Exists(fullpath) 
                    ? assemblyLoadContext.LoadFromAssemblyPath(fullpath) 
                    : null;
            };
        }

        public Runner(IOutputWriter outputWriter)
        {
            _outputWriter = outputWriter;
        }

        public Task RunAsync(string[] args)
        {
            var parser = new Parser(opts =>
            {
                opts.HelpWriter = null;
                opts.CaseInsensitiveEnumValues = true;
            }).ParseArguments<RunnerOptions>(args);
            return parser.WithNotParsed(errors => HandleParseError(parser, errors))
                         .WithParsedAsync(RunAsync);
        }

        public async Task RunAsync(RunnerOptions options)
        {
            try
            {
                await _outputWriter.StartProgressContext(options, async ctx =>
                    {
                        _outputWriter.LogLevel = options.Verbosity;

                        var workspace = await ctx.WithTaskAsync("Loading projects/solutions", task => Workspace.CreateAsync(_outputWriter, options.Solutions, task));
                        await LogWorkspaceSolutionsAsync(workspace);

                        if (options.Undo)
                        {
                            await ctx.WithTaskAsync("Removing ProjectReferences", task => workspace.RemoveReferencesAsync(task));
                            await ctx.WithTaskAsync("Removing projects from solution", task => workspace.CleanupSolutionsAsync(task));
                        }
                        else
                        {
                            await ctx.WithTaskAsync("Adding ProjectReferences for direct references", task => workspace.AddReferencesAsync(task));
                            await ctx.WithTaskAsync("Checking for circular references", task => workspace.CheckForCircularReferences(task));
                            ctx.WithTask("Running NuGet restore", task => workspace.RestoreNugets(options, task));
                            await ctx.WithTaskAsync("Adding ProjectReferences for transitive references", task => workspace.AddTransitiveReferences(options, task));
                            await ctx.WithTaskAsync("Populating solutions", task => workspace.PopulateSolutionsAsync(task));
                        }

                        await ctx.WithTaskAsync("Writing changes", task => workspace.CommitChangesAsync(false, task));
                        _outputWriter.PrintComplete(await workspace.Solutions
                                                                   .Select(sln => sln.Projects.Value)
                                                                   .WhenAll(projects => projects.SelectMany(projs => projs)
                                                                                                .Where(proj => proj.Changes.Any())
                                                                                                .Count()));

                    });
            }
            catch (Exception ex)
            {
                HandleExceptions(ex);
            }
        }

        private void HandleExceptions(Exception exception)
        {
            switch (exception)
            {
                case AmbiguousProjectException:
                case AmbiguousSolutionException:
                case CyclicReferenceException:
                case FileReadException:
                    _outputWriter.PrintException(exception);
                    break;
                case AggregateException aggregateException:
                    aggregateException.InnerExceptions.ForEach(HandleExceptions);
                    break;
                default:
                    _outputWriter.PrintException(exception);
                    ExceptionDispatchInfo.Capture(exception).Throw();
                    break;
            }
        }

        private void HandleParseError(ParserResult<RunnerOptions> parser, IEnumerable<Error> errors)
        {
            if (errors.IsVersion())
            {
                _outputWriter.PrintArgumentMessage(HeadingInfo.Default);
            }
            else
            {
                var helpText = HelpText.AutoBuild(parser, opts =>
                {
                    opts.AdditionalNewLineAfterOption = false;
                    opts.Heading = $"usage: {Assembly.GetEntryAssembly().GetName().Name} [options] <solutions>...";
                    return HelpText.DefaultParsingErrorsHandler(parser, opts);
                }, ex => ex);

                _outputWriter.PrintArgumentMessage(helpText);
            }
        }

        private async Task LogWorkspaceSolutionsAsync(Workspace workspace)
        {
            _outputWriter.PrintTrace("Loaded workspace:");

            foreach (var sln in workspace.Solutions)
            {
                _outputWriter.PrintTrace("- {0}", sln.Filepath);
                foreach (var proj in await sln.Projects.Value)
                {
                    _outputWriter.PrintTrace("  - {0} [{1}]", proj.PackageId, proj.Filepath);
                }
            }
        }
    }
}