namespace OnlineChessGame.Network;

/// <summary>Loads a local .env file without replacing values explicitly set by the OS.</summary>
public static class EnvironmentFile
{
    public static string? LoadForServer()
    {
        // Published applications can keep .env next to the executable.
        string alongsideExecutable = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(alongsideExecutable))
        {
            Load(alongsideExecutable);
            return alongsideExecutable;
        }

        // dotnet run uses the project output directory; look up to the solution root.
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, ".env");
            if (File.Exists(path))
            {
                Load(path);
                return path;
            }

            if (File.Exists(Path.Combine(directory.FullName, "OnlineChessGame.slnx")))
                break;
            directory = directory.Parent;
        }

        // Also support launching from the source root with a custom output directory.
        string currentDirectoryFile = Path.Combine(Environment.CurrentDirectory, ".env");
        if (File.Exists(currentDirectoryFile))
        {
            Load(currentDirectoryFile);
            return currentDirectoryFile;
        }

        return null;
    }

    public static void Load(string path)
    {
        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();

            int separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            string name = line[..separator].Trim();
            if (name.Length == 0 || !IsValidName(name) || Environment.GetEnvironmentVariable(name) is not null)
                continue;

            string value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') ||
                                      (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static bool IsValidName(string name) =>
        (char.IsLetter(name[0]) || name[0] == '_') &&
        name.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
}
