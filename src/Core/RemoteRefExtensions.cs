namespace Devlooped;

public static class RemoteRefExtensions
{
    extension(RemoteRef location)
    {
        public string TempPath => Path.Join(GetTempRoot(), location.Host ?? "github.com", location.Owner, location.Project ?? "", location.Repo, location.Ref ?? "main");

        public string EnsureTempPath() => Directory.CreateUserDirectory(location.TempPath);
    }

    /// <summary>Obtains the temporary directory root, e.g., <c>~/.local/share/dotnet/runfile/</c>.</summary>
    static string GetTempRoot()
    {
        // We use LocalApplicationData (not the system temp folder) on all platforms so that
        // dnx's built-in cleanup of %TEMP% doesn't prune our cached repo downloads mid-run.
        string directory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Directory.CreateUserDirectory(Path.Join(directory, "dotnet", "runfile"));
    }

    /// <summary>Obtains a specific temporary path in a subdirectory of the temp root, e.g., <c>/tmp/dotnet/runfile/{name}</c>.</summary>
    public static string GetTempSubpath(params string[] name) => Directory.CreateUserDirectory(Path.Join([GetTempRoot(), .. name]));
}
