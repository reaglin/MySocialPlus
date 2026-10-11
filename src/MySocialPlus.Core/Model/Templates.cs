using System.Text.Json;
using System.Text.Json.Serialization;

namespace MySocialPlus.Core.Model;

/// <summary>What a template is for.</summary>
public enum TemplateFor { Person, Event, Thing }

/// <summary>A suggested detail in a template.</summary>
public sealed class FieldDef
{
    public string Name { get; set; } = "";
    public DetailKind Kind { get; set; }
    public bool Remind { get; set; }
    /// <summary>A word of help shown under the box: "On the registration card".</summary>
    public string? Hint { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// A kind of person, event or thing — "Car", "Race", "Coffee maker" — and the details worth keeping
/// for it. Templates are data (templates.json), not code: Ron adds a kind, or a detail to a kind,
/// by editing the file or by "Save as a new kind" in the program.
/// </summary>
public sealed class Template
{
    public string Name { get; set; } = "";
    public TemplateFor For { get; set; }
    /// <summary>A heading to group the choices under ("Vehicles", "Home", "Kitchen").</summary>
    public string? Group { get; set; }
    public List<FieldDef> Fields { get; set; } = [];
    /// <summary>What the log is called for this kind ("Service history"); null for no log.</summary>
    public string? LogName { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>Adds the template's details that the node does not have yet, empty, in the template's order.</summary>
    public void ApplyTo(TreeNode node)
    {
        node.TypeName = Name;
        foreach (var f in Fields)
            if (!node.Details.Any(d => string.Equals(d.Name, f.Name, StringComparison.OrdinalIgnoreCase)))
                node.Details.Add(new Detail { Name = f.Name, Kind = f.Kind, Remind = f.Remind || f.Kind == DetailKind.Birthday });
    }

    /// <summary>A new template from a node's details as they are now ("Save as a new kind").</summary>
    public static Template From(TreeNode node, string name, TemplateFor forWhat, string? logName) => new()
    {
        Name = name,
        For = forWhat,
        LogName = logName,
        Fields = node.Details.Select(d => new FieldDef { Name = d.Name, Kind = d.Kind, Remind = d.Remind }).ToList(),
    };
}

public sealed class TemplateSet
{
    /// <summary>Raised in the shipped file when templates are added, so a newer program can add them to Ron's file.</summary>
    public int Version { get; set; }
    public List<Template> Templates { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public IEnumerable<Template> For(TemplateFor f) => Templates.Where(t => t.For == f);

    public Template? Find(string? name, TemplateFor f) =>
        name is null ? null : Templates.FirstOrDefault(t => t.For == f && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
