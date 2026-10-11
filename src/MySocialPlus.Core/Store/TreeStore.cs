using System.Text.Json;
using System.Text.Json.Serialization;
using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Store;

/// <summary>
/// Reads and writes tree.json — small and hand-readable, so it is easy to back up or fix by hand.
/// Writes go to a temporary file first, then replace the old one, so a crash cannot leave half a file.
/// </summary>
public static class TreeStore
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // Empty lists (a topic's details, a person's log) are left out, so the file stays readable.
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                info =>
                {
                    foreach (var p in info.Properties)
                        if (typeof(System.Collections.ICollection).IsAssignableFrom(p.PropertyType) && p.Name != "children" && p.Name != "roots" && p.Name != "templates")
                            p.ShouldSerialize = (_, v) => v is System.Collections.ICollection { Count: > 0 };
                },
            },
        },
    };

    /// <summary>The tree in the file, or a new one with the two roots when there is no file.</summary>
    public static SocialTree Load(string path)
    {
        if (!File.Exists(path)) return SocialTree.CreateNew();
        var tree = JsonSerializer.Deserialize<SocialTree>(File.ReadAllText(path), Options)
                   ?? SocialTree.CreateNew();
        tree.EnsureRoots();
        return tree;
    }

    public static string ToJson(SocialTree tree) => JsonSerializer.Serialize(tree, Options);

    public static SocialTree FromJson(string json)
    {
        var tree = JsonSerializer.Deserialize<SocialTree>(json, Options) ?? SocialTree.CreateNew();
        tree.EnsureRoots();
        return tree;
    }

    public static void Save(SocialTree tree, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, ToJson(tree));
        File.Move(temp, path, overwrite: true);
    }
}
