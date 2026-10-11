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

    /// <summary>The second, smaller line: an account's handle, an event's date, a thing's kind.</summary>
    public string? Detail => Node.Kind switch
    {
        NodeKind.Account when Node.Account is { } a => a.IsMine ? a.Handle : $"{a.Info.Name} · {a.Handle}",
        NodeKind.Event when Node.Event?.NextOn(DateOnly.FromDateTime(DateTime.Today)) is { } next =>
            next.ToString(Node.Event.AllDay ? "ddd MMM d" : "ddd MMM d · h:mm tt", System.Globalization.CultureInfo.CurrentCulture),
        NodeKind.Event when Node.Event?.Start is { } past => "was " + past.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture),
        NodeKind.Event => "no date yet",
        NodeKind.Thing => Node.TypeName,
        _ => null,
    };

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
        NodeKind.Root when Node.Id == SocialTree.MyEventsId => "",  // calendar
        NodeKind.Root when Node.Id == SocialTree.MyThingsId => "",  // home
        NodeKind.Root => "",                                         // people
        NodeKind.Topic => "",                                        // folder
        NodeKind.Event => "",                                        // calendar
        NodeKind.Thing => "",                                        // package
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
        NodeKind.Root when Node.Id == SocialTree.MyEventsId => "Things you are going to: races, concerts, classes, trips — with who is going and the plan.",
        NodeKind.Root when Node.Id == SocialTree.MyThingsId => "Things you own: their details, reminders (renewals, warranties) and history.",
        NodeKind.Root => "The people in your life, in topics you make. Drag a person onto a topic to move them.",
        NodeKind.Topic when Node.Root.Id == SocialTree.MyFriendsId => "A topic you made. Its number is everything new from the people in it.",
        NodeKind.Topic => "A topic you made. Drag things onto it to group them.",
        NodeKind.Event => "Open to see when, where, who is going and the agenda.",
        NodeKind.Thing => "Open to see its details and history.",
        _ when HasProblem => "Could not be read last time: " + ProblemText,
        NodeKind.Person => "Open to see what you keep about them, their profile link, and their posts.",
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
