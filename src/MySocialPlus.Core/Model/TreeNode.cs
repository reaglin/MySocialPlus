using System.Text.Json;
using System.Text.Json.Serialization;

namespace MySocialPlus.Core.Model;

/// <summary>Saved by name in tree.json, so new kinds go at the end and none is renamed.</summary>
public enum NodeKind { Root, Topic, Person, Account, Event, Thing }

/// <summary>How a node's content is listed. Time order either way — never ranked (rule 2).</summary>
public enum SortOrder { NewestFirst, OldestUnreadFirst }

/// <summary>
/// One node of Ron's tree, saved in tree.json. Four roots: My Posts (Ron's own accounts), My Friends
/// (topics and people; people hold their accounts), My Events and My Things (topics, events, things).
/// Posts are not nodes in the file — they live in the item cache and are listed in the content pane.
/// People, events and things carry <see cref="Details"/> (name/value pairs) and a <see cref="Log"/>,
/// so new kinds of information need no code (Ron, 2026-10-11).
/// </summary>
public sealed class TreeNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public NodeKind Kind { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Only on Account nodes.</summary>
    public Account? Account { get; set; }

    /// <summary>The kind of person, event or thing ("Car", "Race", "Friend") — the template it started from.</summary>
    public string? TypeName { get; set; }

    /// <summary>What Ron keeps about it: birthday, license plate, bib number… in his order.</summary>
    public List<Detail> Details { get; set; } = [];

    /// <summary>Dated entries: a car's services, a house's repairs, notes from a talk with a friend.</summary>
    public List<LogEntry> Log { get; set; } = [];

    /// <summary>Only on Event nodes: when, where, who with.</summary>
    public EventInfo? Event { get; set; }

    /// <summary>Null means the program's default (newest first).</summary>
    public SortOrder? Sort { get; set; }

    /// <summary>Whether the node is open in the tree, so the tree comes back as Ron left it.</summary>
    public bool Expanded { get; set; }

    public List<TreeNode> Children { get; set; } = [];

    /// <summary>Fields written by a newer version, kept so saving never loses them.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore] public TreeNode? Parent { get; internal set; }

    public static TreeNode Topic(string name) => new() { Kind = NodeKind.Topic, Name = name, Expanded = true };
    public static TreeNode Person(string name) => new() { Kind = NodeKind.Person, Name = name };
    public static TreeNode ForAccount(Account a, string? name = null) =>
        new() { Kind = NodeKind.Account, Name = name ?? a.Info.Name, Account = a };
    public static TreeNode NewEvent(string name, DateTime? start = null) =>
        new() { Kind = NodeKind.Event, Name = name, Event = new EventInfo { Start = start } };
    public static TreeNode Thing(string name) => new() { Kind = NodeKind.Thing, Name = name };

    /// <summary>A detail's value by name (case does not matter), or null.</summary>
    public string? Get(string detail) =>
        Details.FirstOrDefault(d => string.Equals(d.Name, detail, StringComparison.OrdinalIgnoreCase))?.Value is { Length: > 0 } v ? v : null;

    /// <summary>Sets a detail, adding it at the end if it is not there yet.</summary>
    public Detail Set(string detail, string value, DetailKind kind = DetailKind.Text)
    {
        var d = Details.FirstOrDefault(x => string.Equals(x.Name, detail, StringComparison.OrdinalIgnoreCase));
        if (d is null) Details.Add(d = new Detail { Name = detail, Kind = kind });
        d.Value = value;
        return d;
    }

    /// <summary>This node and everything beneath it.</summary>
    public IEnumerable<TreeNode> SelfAndDescendants()
    {
        yield return this;
        foreach (var c in Children)
            foreach (var d in c.SelfAndDescendants())
                yield return d;
    }

    /// <summary>The cache keys of every readable account at or beneath this node.</summary>
    public IReadOnlyList<string> AccountKeys() =>
        SelfAndDescendants().Where(n => n.Account is { IsReadable: true })
                            .Select(n => n.Account!.Key).Distinct().ToList();

    public bool IsAncestorOf(TreeNode other)
    {
        for (var p = other.Parent; p is not null; p = p.Parent)
            if (ReferenceEquals(p, this)) return true;
        return false;
    }

    /// <summary>The root this node is under (itself for a root).</summary>
    [JsonIgnore]
    public TreeNode Root
    {
        get { var n = this; while (n.Parent is not null) n = n.Parent; return n; }
    }

    public override string ToString() => $"{Kind} {Name}";
}

/// <summary>
/// One account on one network. <see cref="Id"/> is the network's own stable id (a Bluesky DID)
/// when known; a handle can change, so the key prefers the id.
/// </summary>
public sealed class Account
{
    public Network Network { get; set; }
    public string Handle { get; set; } = "";
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>True for Ron's own accounts (under My Posts): activity on his posts is read for these.</summary>
    public bool IsMine { get; set; }

    /// <summary>When MySocial+ last read this account successfully. Null until the first read.</summary>
    public DateTimeOffset? LastChecked { get; set; }

    /// <summary>What went wrong the last time it was read, in words for Ron; null when it worked.</summary>
    public string? LastError { get; set; }

    /// <summary>What kind of problem LastError is (Sources.SourceProblem by name), so the window can offer the right fix.</summary>
    public string? LastProblem { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string Key => $"{Network.ToString().ToLowerInvariant()}:{(Id ?? Handle).Trim().ToLowerInvariant()}";

    [JsonIgnore] public NetworkInfo Info => NetworkInfo.For(Network);

    /// <summary>Whether MySocial+ can read anything for this account (and so give it a badge).</summary>
    [JsonIgnore] public bool IsReadable => IsMine ? Info.CanReadMyPosts : Info.CanReadFriends;

    [JsonIgnore] public string? ProfileUrl => Info.ProfileUrl(Handle);
}
