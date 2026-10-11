namespace MySocialPlus.Core.Store;

/// <summary>
/// Where things are kept (CLAUDE.md, rule 6). Ron's tree and the cache of posts go where people
/// look — <c>Documents\MySocialPlus</c>; sign-in secrets go where nobody does —
/// <c>%LOCALAPPDATA%\MySocialPlus</c>, DPAPI encrypted for this Windows user.
/// </summary>
public sealed class DataPaths
{
    public DataPaths(string dataFolder, string secretsFolder)
    {
        DataFolder = dataFolder;
        SecretsFolder = secretsFolder;
    }

    public static DataPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MySocialPlus"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MySocialPlus"));

    /// <summary>A separate folder (the sample, a test): its secrets stay beside it so nothing real is touched.</summary>
    public static DataPaths InFolder(string folder) => new(folder, Path.Combine(folder, "secrets"));

    public string DataFolder { get; }
    public string SecretsFolder { get; }
    public string TreeFile => Path.Combine(DataFolder, "tree.json");
    public string ItemsDb => Path.Combine(DataFolder, "items.db");
    public string SettingsFile => Path.Combine(DataFolder, "settings.json");
}
