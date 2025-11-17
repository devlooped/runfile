#:package Spectre.Console@0.*
#:package ConsoleAppFramework@5.*
#:property Nullable=enable
#:property ImportAvalonia=false

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ConsoleAppFramework;
using Spectre.Console;

ConsoleApp.Run(args, Check);

/// <summary>Checks whether the GCM and Avalonia dependencies align properly in a project.</summary>
/// <param name="project">The MSBuild project file to check.</param>
/// <param name="update">Project update on mismatch is not a failure.</param>
int Check([Argument] string project, bool update)
{
    if (!File.Exists(project))
    {
        AnsiConsole.MarkupLine($"[red]Project file '{project}' does not exist![/]");
        return 1;
    }

    var doc = XDocument.Load(project, LoadOptions.PreserveWhitespace);
    var gcm = doc.Descendants("PackageReference")
        .FirstOrDefault(pr => pr.Attribute("Include")?.Value == "git-credential-manager")
        ?.Attribute("Version")?.Value;

    if (gcm == null)
    {
        AnsiConsole.MarkupLine($"[red]Could not find git-credential-manager package reference in '{project}'[/]");
        return 1;
    }

    var avalonia = doc.Descendants("PackageReference")
        .FirstOrDefault(pr => pr.Attribute("Include")?.Value.StartsWith("Avalonia") == true)
        ?.Attribute("Version")?.Value;

    if (avalonia == null)
    {
        AnsiConsole.MarkupLine($"[yellow]Could not find Avalonia package reference in '{project}'[/]");
        return 0;
    }

    var dependency = ReadAvaloniaDependency(gcm);
    var existing = dependency;

    foreach (XElement element in doc.Descendants("PackageReference").Where(pr => pr.Attribute("Include")?.Value.StartsWith("Avalonia") == true))
    {
        if (element.Attribute("Version")?.Value != dependency)
        {
            existing = element.Attribute("Version")?.Value;
            element.SetAttributeValue("Version", dependency);
        }
    }

    if (existing != dependency)
    {
        doc.Save(project, SaveOptions.DisableFormatting);
        AnsiConsole.MarkupLine($":{(update ? "check_mark_button" : "cross_mark")}: Updated Avalonia dependency [yellow]v{existing}[/] to [green]v{avalonia}[/] in [underline blue][link={project}]{Path.GetFileName(project)}[/][/].");
        return update ? 1 : 0;
    }
    else
    {
        AnsiConsole.MarkupLine($":check_mark: [grey]Avalonia dependency is already at v{avalonia} in [underline blue][link={project}]{Path.GetFileName(project)}[/][/]. No update needed.[/]");
        return 0;
    }
}

static string ReadAvaloniaDependency(string gcm)
{
    var url = $"https://github.com/git-ecosystem/git-credential-manager/raw/refs/tags/v{gcm}/src/shared/Core/Core.csproj";
    var doc = XDocument.Load(url);

    var dependency = doc.Descendants("PackageReference")
        .First(pr => pr.Attribute("Include")?.Value == "Avalonia")?
        .Attribute("Version")?.Value;

    return dependency ?? throw new ArgumentException("Could not find Avalonia package reference.");
}