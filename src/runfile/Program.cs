using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Devlooped;
using DotNetConfig;
using GitCredentialManager.UI;
using Spectre.Console;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;

// Default PublishAot=false since it severely limits the code that can run (i.e. no reflection, STJ even)
var aot = false;
if (args.Any(x => x is "--aot" or "--dnx-aot"))
{
    aot = true;
    args = [.. args.Where(x => x is not "--aot" and not "--dnx-aot")];
}

// --dnx-debug to launch debugger before running
var debug = false;
if (args.Any(x => x == "--dnx-debug"))
{
    debug = true;
    args = [.. args.Where(x => x != "--dnx-debug")];
}

// --dnx-force to skip ETag checking
var force = false;
if (args.Any(x => x == "--dnx-force"))
{
    force = true;
    args = [.. args.Where(x => x != "--dnx-force")];
}

var config = Config.Build(Config.GlobalLocation);
if (args.Length > 0 && config.GetString("runfile", args[0]) is string aliased)
    args = [aliased, .. args[1..]];

// Set alias and remove from args if present (--alias or --dnx-alias)
var aliasOption = new Option<string?>("--alias");
aliasOption.Aliases.Add("--dnx-alias");
var parsed = new RootCommand() { Options = { aliasOption } }.Parse(args);
var alias = parsed.GetValue(aliasOption);
if (alias != null)
    args = [.. parsed.UnmatchedTokens];

if (args.Length == 0 || !RemoteRef.TryParse(args[0], out var location))
{
    AnsiConsole.MarkupLine(
        $"""
        Usage:
            [grey][[dnx]][/] [lime]{ThisAssembly.Project.ToolCommandName}[/] [grey][[OPTIONS]][/] [bold]<repoRef>[/] [grey italic][[<appArgs>...]][/]

        Arguments:
            [bold]<REPO_REF>[/]  Reference to remote file to run, with format [yellow][[host/]]owner/repo[[@ref]][[:path]][/]
                        [italic][yellow]host[/][/] optional host name ([grey][[gist.]][/]github.com|gitlab.com|dev.azure.com, default: github.com)
                        [italic][yellow]@ref[/][/] optional branch, tag, or commit (default: default branch)
                        [italic][yellow]:path[/][/] optional path to file in repo (default: program.cs at repo root)
                  
                        Examples: 
                        * kzu/sandbox@v1.0.0:run.cs           (implied host github.com, explicit tag and file path)
                        * gitlab.com/kzu/sandbox@main:run.cs  (all explicit parts)
                        * kzu/sandbox                         (implied host github.com, ref and path defaults)
                  
                        If --dnx-alias was used in a previous run, the alias can be used instead of the full ref.

            [bold]<appArgs>[/]   Arguments passed to the C# program that is being run. 

        Options:
            [bold]--dnx-aot[/]         Enable dotnet AOT defaults for run file.cs. Defaults to false.
            [bold]--dnx-alias[/] ALIAS Assign an alias on first usage which can be used instead of the full ref.
            [bold]--dnx-debug[/]       Launch the debugger before running.
            [bold]--dnx-force[/]       Force download, skipping ETag checking.
        """);
    return;
}

if (alias != null)
    config = config.SetString("runfile", alias, location.ToString());

// Launch debugger if --dnx-debug was specified
if (debug)
    Debugger.Launch();

// Create the dispatcher on the main thread. This is required
// for some platform UI services such as macOS that mandates
// all controls are created/accessed on the initial thread
// created by the process (the process entry thread).
Dispatcher.Initialize();

// Run AppMain in a new thread and keep the main thread free
// to process the dispatcher's job queue.
var main = Task
    .Run(() => new RemoteRunner(location, ThisAssembly.Project.ToolCommandName, config)
    .RunAsync(args[1..], aot, force))
    .ContinueWith(t =>
    {
        Dispatcher.MainThread.Shutdown();
        return t.Result;
    });

// Process the dispatcher job queue (aka: message pump, run-loop, etc...)
// We must ensure to run this on the same thread that it was created on
// (the main thread) so we cannot use any async/await calls between
// Dispatcher.Initialize and Run.
Dispatcher.MainThread.Run();

// Dispatcher was shutdown
Environment.Exit(await main);
