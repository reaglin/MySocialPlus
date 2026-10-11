using System.Text.Json;
using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Store;

/// <summary>
/// templates.json in the data folder: the kinds of people, events and things, and their suggested
/// details. The first run writes the shipped set; after that the file is Ron's — the program only
/// adds kinds it ships later (by <see cref="TemplateSet.Version"/>) and never removes or changes his.
/// </summary>
public static class TemplateStore
{
    public static TemplateSet Shipped()
    {
        using var s = typeof(TemplateStore).Assembly.GetManifestResourceStream("MySocialPlus.Core.Templates.default-templates.json")
                      ?? throw new InvalidOperationException("The shipped templates are missing from the program.");
        return JsonSerializer.Deserialize<TemplateSet>(s, TreeStore.Options) ?? new TemplateSet();
    }

    public static TemplateSet Load(string path)
    {
        var shipped = Shipped();
        if (!File.Exists(path))
        {
            Save(shipped, path);
            return shipped;
        }
        var mine = JsonSerializer.Deserialize<TemplateSet>(File.ReadAllText(path), TreeStore.Options) ?? new TemplateSet();
        if (mine.Version < shipped.Version)
        {
            foreach (var t in shipped.Templates)
                if (mine.Find(t.Name, t.For) is null) mine.Templates.Add(t);
            mine.Version = shipped.Version;
            Save(mine, path);
        }
        return mine;
    }

    public static void Save(TemplateSet set, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(set, TreeStore.Options));
        File.Move(temp, path, overwrite: true);
    }
}
