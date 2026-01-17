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

RemoteRef? location = default;
var validRef = args.Length > 0 &&
    (RemoteRef.TryParse("gist.github.com/" + args[0], out location) ||
    RemoteRef.TryParse(args[0], out location));

if (args.Length == 0 || !validRef || location is null)
{
    AnsiConsole.MarkupLine(
        $"""
        Usage:
            [grey][[dnx]][/] [lime]{ThisAssembly.Project.ToolCommandName}[/] [grey][[OPTIONS]][/] [bold]<gistRef>[/] [grey italic][[<appArgs>...]][/]

        Arguments:
            [bold]<GIST_REF>[/]  Reference to gist file to run, with format [yellow]owner/gist[[@commit]][[:path]][/]
                        [italic][yellow]@commit[/][/] optional gist commit (default: latest)
                        [italic][yellow]:path[/][/] optional path to file in gist (default: program.cs or first .cs file)
                       
                        Examples: 
                        * kzu/0ac826dc7de666546aaedd38e5965381                 (tip commit and program.cs or first .cs file)
                        * kzu/0ac826dc7de666546aaedd38e5965381@d8079cf:run.cs  (explicit commit and file path)
                        
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