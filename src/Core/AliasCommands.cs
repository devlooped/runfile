using DotNetConfig;
using Spectre.Console;

namespace Devlooped;

public static class AliasCommands
{
    public const string CommandName = "alias";
    internal const string Section = "runfile";

    public static int Execute(string[] args, Config config)
    {
        if (args.Length == 0)
            return List(config);

        if (args[0] is "delete")
            return Delete(args, config);

        if (args[0] is "rename")
            return Rename(args, config);

        ShowUsage();
        return 1;
    }

    /// <summary>Writes the alias table when any are configured. Returns whether a table was written.</summary>
    public static bool WriteTableIfAny(Config config)
    {
        var aliases = GetAliases(config);
        if (aliases.Length == 0)
            return false;

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Aliases:");

        WriteTable(aliases);
        return true;
    }

    static int List(Config config)
    {
        var aliases = GetAliases(config);
        if (aliases.Length == 0)
        {
            AnsiConsole.MarkupLine(":information_source: [grey]No aliases configured. Use[/] [yellow]--dnx-alias[/] [grey]to create one.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[grey]Configured ref aliases[/] [dim]({aliases.Length})[/]:");
        WriteTable(aliases);
        return 0;
    }

    static int Delete(string[] args, Config config)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            AnsiConsole.MarkupLine(":cross_mark: [red]Missing alias name.[/]");
            ShowUsage();
            return 1;
        }

        var name = args[1];
        if (config.GetString(Section, name) is null)
        {
            AnsiConsole.MarkupLine($":cross_mark: [red]Alias[/] [cyan bold]{Markup.Escape(name)}[/] [red]not found.[/]");
            return 1;
        }

        config.Unset(Section, name);
        AnsiConsole.MarkupLine($":check_mark_button: [green]Deleted alias[/] [cyan bold]{Markup.Escape(name)}[/][green].[/]");
        return 0;
    }

    static int Rename(string[] args, Config config)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
        {
            AnsiConsole.MarkupLine(":cross_mark: [red]Missing alias name.[/]");
            ShowUsage();
            return 1;
        }

        var oldName = args[1];
        var newName = args[2];

        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($":information_source: [grey]Alias[/] [cyan bold]{Markup.Escape(oldName)}[/] [grey]already has that name.[/]");
            return 0;
        }

        if (config.GetString(Section, oldName) is not { } @ref)
        {
            AnsiConsole.MarkupLine($":cross_mark: [red]Alias[/] [cyan bold]{Markup.Escape(oldName)}[/] [red]not found.[/]");
            return 1;
        }

        if (config.GetString(Section, newName) is not null)
        {
            AnsiConsole.MarkupLine($":cross_mark: [red]Alias[/] [cyan bold]{Markup.Escape(newName)}[/] [red]already exists.[/]");
            return 1;
        }

        config.SetString(Section, newName, @ref).Unset(Section, oldName);
        AnsiConsole.MarkupLine($":check_mark_button: [green]Renamed alias[/] [cyan bold]{Markup.Escape(oldName)}[/] [green]to[/] [cyan bold]{Markup.Escape(newName)}[/][green].[/]");
        return 0;
    }

    static (string Name, string Ref)[] GetAliases(Config config) =>
        config
            .Where(entry => entry.Section == Section && entry.Subsection == null)
            .Select(entry => entry.Variable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => (Name: name, Ref: config.GetString(Section, name)!))
            .Where(alias => alias.Ref != null)
            .ToArray();

    static void WriteTable((string Name, string Ref)[] aliases)
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey23)
            .AddColumn(new TableColumn("[lime bold]:label: Alias[/]").LeftAligned())
            .AddColumn(new TableColumn("[lime bold]:link: Ref[/]").LeftAligned());

        foreach (var (name, @ref) in aliases)
            table.AddRow($"[cyan bold]{Markup.Escape(name)}[/]", FormatRefLink(@ref));

        AnsiConsole.Write(table);
    }

    static string FormatRef(string @ref) =>
        @ref.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase)
            ? @ref["github.com/".Length..]
            : @ref;

    static string FormatRefLink(string @ref)
    {
        var display = Markup.Escape(FormatRef(@ref));
        return RemoteRef.TryParse(@ref, out var parsed)
            ? $"[link={parsed.ToWebUrl()}]{display}[/]"
            : $"[link]{display}[/]";
    }

    static void ShowUsage() =>
        AnsiConsole.MarkupLine(
            """
            [grey]Usage:[/]
                [cyan bold]alias[/]                        [grey]List configured ref aliases.[/]
                [cyan bold]alias delete[/] [yellow]NAME[/]        [grey]Delete a ref alias.[/]
                [cyan bold]alias rename[/] [yellow]OLD[/] [yellow]NEW[/]  [grey]Rename a ref alias.[/]
            """);
}
