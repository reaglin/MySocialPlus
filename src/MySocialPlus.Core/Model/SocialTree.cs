using System.Text.Json;
using System.Text.Json.Serialization;

namespace MySocialPlus.Core.Model;

/// <summary>
/// Ron's whole tree: the roots in order, starting with My Posts and My Friends. Every change goes
/// through here so parent links stay right and the rules hold (people go in topics, accounts go in
/// people or under My Posts, roots stay put).
/// </summary>
public sealed class SocialTree
{
    public const string MyPostsId = "my-posts";
    public const string MyFriendsId = "my-friends";

    public int Version { get; set; } = 1;
    public List<TreeNode> Roots { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore] public TreeNode MyPosts => Roots.First(r => r.Id == MyPostsId);
    [JsonIgnore] public TreeNode MyFriends => Roots.First(r => r.Id == MyFriendsId);

    public static SocialTree CreateNew()
    {
        var t = new SocialTree();
        t.EnsureRoots();
        return t;
    }

    /// <summary>Puts back the two roots if the file lacks them, and sets every parent link.</summary>
    public void EnsureRoots()
    {
        if (!Roots.Any(r => r.Id == MyPostsId))
            Roots.Insert(0, new TreeNode { Id = MyPostsId, Kind = NodeKind.Root, Name = "My Posts", Expanded = true });
        if (!Roots.Any(r => r.Id == MyFriendsId))
            Roots.Insert(1, new TreeNode { Id = MyFriendsId, Kind = NodeKind.Root, Name = "My Friends", Expanded = true });
        foreach (var r in Roots) { r.Parent = null; Relink(r); }
    }

    private static void Relink(TreeNode n)
    {
        foreach (var c in n.Children) { c.Parent = n; Relink(c); }
    }

    public IEnumerable<TreeNode> AllNodes() => Roots.SelectMany(r => r.SelfAndDescendants());

    public TreeNode? Find(string id) => AllNodes().FirstOrDefault(n => n.Id == id);

    /// <summary>Every account in the tree (an account may appear only once per key).</summary>
    public IEnumerable<Account> Accounts() => AllNodes().Where(n => n.Account is not null).Select(n => n.Account!);

    /// <summary>Whether <paramref name="child"/> may go under <paramref name="parent"/>, and if not, why.</summary>
    public static string? WhyNotAllowed(TreeNode parent, TreeNode child)
    {
        if (ReferenceEquals(parent, child) || child.IsAncestorOf(parent))
            return "A node cannot go inside itself.";
        if (child.Kind == NodeKind.Root) return "My Posts and My Friends stay at the top.";
        var root = parent.Root;
        return child.Kind switch
        {
            NodeKind.Topic when parent.Kind is NodeKind.Root or NodeKind.Topic && root.Id != MyPostsId => null,
            NodeKind.Topic => "Topics go under My Friends or inside another topic.",
            NodeKind.Person when parent.Kind is NodeKind.Root or NodeKind.Topic && root.Id != MyPostsId => null,
            NodeKind.Person => "People go under My Friends or inside a topic.",
            NodeKind.Account when parent.Kind == NodeKind.Person => null,
            NodeKind.Account when parent.Id == MyPostsId => null,
            NodeKind.Account => "An account goes inside a person, or under My Posts if it is yours.",
            _ => "That cannot go there.",
        };
    }

    public void Add(TreeNode parent, TreeNode child, int index = -1)
    {
        var why = WhyNotAllowed(parent, child);
        if (why is not null) throw new InvalidOperationException(why);
        if (child.Account is not null)
        {
            child.Account.IsMine = parent.Id == MyPostsId;
            if (Accounts().Any(a => !ReferenceEquals(a, child.Account) && a.Key == child.Account.Key))
                throw new InvalidOperationException($"{child.Account.Handle} on {child.Account.Info.Name} is already in the tree.");
        }
        child.Parent?.Children.Remove(child);
        if (index < 0 || index > parent.Children.Count) parent.Children.Add(child);
        else parent.Children.Insert(index, child);
        child.Parent = parent;
        Relink(child);
    }

    /// <summary>Moves a node to a new parent (a person dragged to another topic).</summary>
    public void Move(TreeNode node, TreeNode newParent, int index = -1)
    {
        var why = WhyNotAllowed(newParent, node);
        if (why is not null) throw new InvalidOperationException(why);
        if (node.Account is not null && (node.Parent?.Id == MyPostsId) != (newParent.Id == MyPostsId))
            throw new InvalidOperationException("Your own accounts stay under My Posts, and friends' accounts stay with the person.");
        var old = node.Parent;
        if (old is not null)
        {
            var oldIndex = old.Children.IndexOf(node);
            old.Children.RemoveAt(oldIndex);
            if (ReferenceEquals(old, newParent) && index > oldIndex) index--;
        }
        if (index < 0 || index > newParent.Children.Count) newParent.Children.Add(node);
        else newParent.Children.Insert(index, node);
        node.Parent = newParent;
    }

    public static void Rename(TreeNode node, string name)
    {
        if (node.Kind == NodeKind.Root) throw new InvalidOperationException("My Posts and My Friends keep their names.");
        name = name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Type a name.");
        node.Name = name;
    }

    public void Delete(TreeNode node)
    {
        if (node.Kind == NodeKind.Root) throw new InvalidOperationException("My Posts and My Friends cannot be deleted.");
        node.Parent?.Children.Remove(node);
        node.Parent = null;
    }

    /// <summary>The person an account node belongs to, or null under My Posts.</summary>
    public static TreeNode? PersonOf(TreeNode node)
    {
        for (var n = node; n is not null; n = n.Parent)
            if (n.Kind == NodeKind.Person) return n;
        return null;
    }
}
