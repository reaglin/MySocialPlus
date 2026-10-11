using System.Text.Json;
using System.Text.Json.Serialization;

namespace MySocialPlus.Core.Model;

public enum NodeKind { Root, Topic, Person, Account }

/// <summary>How a node's content is listed. Time order either way — never ranked (rule 2).</summary>
public enum SortOrder { NewestFirst, OldestUnreadFirst }

/// <summary>
/// One node of Ron's tree, saved in tree.json. Roots hold topics and people (My Friends) or Ron's
/// own accounts (My Posts); topics hold topics and people; people hold their accounts. Posts are
/// not nodes in the file — they live in the item cache and are listed in the content pane.
/// </summary>
public sealed class TreeNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public NodeKind Kind { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Only on Account nodes.</summary>
    public Account? Account { get; set; }

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
