using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MySocialPlus.Core.Model;

namespace MySocialPlus.App.ViewModels;

/// <summary>One node in the window's tree: the model node plus its badge and open/selected state.</summary>
public sealed partial class NodeVm : ObservableObject
{
    public NodeVm(TreeNode node, NodeVm? parent)
    {
        Node = node;
        Parent = parent;
        foreach (var c in node.Children) Children.Add(new NodeVm(c, this));
    }

    public TreeNode Node { get; }
    public NodeVm? Parent { get; }
    public ObservableCollection<NodeVm> Children { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge), nameof(BadgeText), nameof(SpokenName))]
    private int _badge;

    [ObservableProperty] private bool _isSelected;

    public bool IsExpanded
    {
        get => Node.Expanded;
        set { if (Node.Expanded != value) { Node.Expanded = value; OnPropertyChanged(); ExpandedChanged?.Invoke(this, EventArgs.Empty); } }
    }

    /// <summary>Raised so the window can save the tree with the node left open or closed.</summary>
    public static event EventHandler? ExpandedChanged;

    public string Name => Node.Kind == NodeKind.Account && Node.Account is { } a && a.IsMine
        ? a.Info.Name
        : Node.Name;

    /// <summary>The second, smaller line: an account's handle.</summary>
    public string? Detail => Node.Account is { } a ? (Node.Account.IsMine ? a.Handle : $"{a.Info.Name} · {a.Handle}") : null;

    public bool HasBadge => Badge > 0;
    public string BadgeText => Badge > 99 ? "99+" : Badge.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>An account MySocial+ cannot read: shown dimmer, never badged, and the tooltip says why.</summary>
    public bool IsProfileOnly => Node.Account is { IsReadable: false };

    /// <summary>The account, or for a person any of their accounts, could not be read last time.</summary>
    public bool HasProblem => Node.Kind is NodeKind.Account or NodeKind.Person &&
                              Node.SelfAndDescendants().Any(n => n.Account is { IsReadable: true, LastError: not null });

    private string? ProblemText => Node.SelfAndDescendants().Select(n => n.Account?.LastError).FirstOrDefault(e => e is not null);

    /// <summary>Segoe MDL2 Assets glyphs.</summary>
    public string Glyph => Node.Kind switch
    {
        NodeKind.Root when Node.Id == SocialTree.MyPostsId => "",   // edit (what I wrote)
        NodeKind.Root => "",                                         // people
        NodeKind.Topic => "",                                        // folder
        NodeKind.Person when HasProblem => "",                       // warning
        NodeKind.Person => "",                                       // contact
        _ when IsProfileOnly => "",                                  // link: opens their profile
        _ when HasProblem => "",
        _ => "",                                                     // globe: a network account
    };

    public string ToolTip => Node.Kind switch
    {
        NodeKind.Root when Node.Id == SocialTree.MyPostsId =>
            "Your own accounts. Open one to see your posts and who replied, liked or shared them.",
        NodeKind.Root => "The people you follow, in topics you make. Drag a person onto a topic to move them.",
        NodeKind.Topic => "A topic you made. Its number is everything new from the people in it.",
        _ when HasProblem => "Could not be read last time: " + ProblemText,
        NodeKind.Person => "Open to see everything they posted, on every network, newest first.",
        _ when IsProfileOnly => Node.Account!.Info.WhyNot,
        _ => "Open to see this account's posts.",
    };

    public string SpokenName => Badge > 0 ? $"{Name}, {Badge} new" : Name;

    /// <summary>"Family ▸ Sarah Lee" — where the node is, for the What's new list.</summary>
    public string Path
    {
        get
        {
            var parts = new List<string>();
            for (var v = this; v is not null; v = v.Parent) parts.Insert(0, v.Name);
            if (parts.Count > 1 && Node.Root.Id == SocialTree.MyFriendsId) parts.RemoveAt(0);   // "My Friends ▸" says nothing
            return string.Join(" ▸ ", parts);
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(ToolTip));
        OnPropertyChanged(nameof(HasProblem));
        OnPropertyChanged(nameof(SpokenName));
    }

    public IEnumerable<NodeVm> SelfAndDescendants()
    {
        yield return this;
        foreach (var c in Children)
            foreach (var d in c.SelfAndDescendants()) yield return d;
    }
}
