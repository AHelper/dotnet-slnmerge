using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Build.Locator;
using CommandLine.Text;
using System.Reflection;
using System.Collections;
using System.Runtime.ExceptionServices;

namespace AHelper.SlnMerge.Core
{
    public class RunnerOptions
    {
        [Value(0, MetaName = "solutions", HelpText = "List of solutions", Required = true)]
        public IList<string> Solutions { get; set; }

        [Option('p', "property", HelpText = "MSBuild property (key=value) when loading projects")]
        public IEnumerable<string> PropertyStrings { get; set; }

        public IDictionary<string, string> Properties => PropertyStrings.Select(str => str.Split('=', 2))
                                                                        .Where(pair => pair.Length == 2)
                                                                        .ToDictionary(pair => pair[0], pair => pair[1]);
    }

    public class Runner
    {
        private readonly IOutputWriter _outputWriter;

        static Runner()
        {
            MSBuildLocator.RegisterDefaults();
        }

        public Runner(IOutputWriter outputWriter)
        {
            _outputWriter = outputWriter;
        }

        public Task RunAsync(string[] args)
        {
            var parser = new Parser(opts => opts.HelpWriter = null).ParseArguments<RunnerOptions>(args);
            return parser.WithNotParsed(errors => HandleParseError(parser, errors))
                         .WithParsedAsync(RunAsync);
        }

        public async Task RunAsync(RunnerOptions options)
        {
            try
            {
                var workspace = await Workspace.CreateAsync(_outputWriter, options.Solutions);
                await LogWorkspaceSolutionsAsync(workspace);

                await workspace.AddReferencesAsync();
                await workspace.CheckForCircularReferences();
                await workspace.PopulateSolutionsAsync();
                await workspace.CommitChangesAsync(false);
                _outputWriter.PrintComplete(workspace.Solutions.SelectMany(sln => sln.Changes.Select(change => change.Key))
                                                               .Distinct()
                                                               .Count());
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
                case FileReadException:
                case AmbiguousProjectException:
                case CyclicReferenceException:
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