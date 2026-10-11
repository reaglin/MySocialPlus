using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MySocialPlus.App.Windows;
using MySocialPlus.Core.Model;

namespace MySocialPlus.App.Views;

/// <summary>
/// My Events, My Things, a topic under them, or a topic of friends: what is inside, one line each,
/// with a button to add another. Events come as Upcoming (soonest first) and Past.
/// </summary>
public sealed class GroupView : UserControl
{
    private readonly StackPanel _body = new() { Margin = new Thickness(20, 12, 20, 24), MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left };

    public event Action<TreeNode>? OpenNode;
    /// <summary>"New event…", "New thing…" or "Add person…" pressed, with the group to add to.</summary>
    public event Action<TreeNode>? AddHere;
    public event Action<TreeNode>? NewTopicHere;

    public GroupView()
    {
        Content = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public void Show(TreeNode group)
    {
        _body.Children.Clear();
        var root = group.Root.Id;
        var (addText, what) = root switch
        {
            SocialTree.MyEventsId => ("New event…", "events"),
            SocialTree.MyThingsId => ("New thing…", "things"),
            _ => ("Add person…", "people"),
        };
        var add = new Button { Content = addText, Style = (Style)Application.Current.FindResource("Primary"), Margin = new Thickness(0, 0, 8, 0) };
        add.Click += (_, _) => AddHere?.Invoke(group);
        var topic = new Button { Content = "New topic…", ToolTip = $"A group inside {group.Name}, like Running or Kitchen" };
        topic.Click += (_, _) => NewTopicHere?.Invoke(group);
        _body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8), Children = { add, topic } });

        var today = DateOnly.FromDateTime(DateTime.Today);
        switch (root)
        {
            case SocialTree.MyEventsId:
                var events = group.SelfAndDescendants().Where(n => n.Kind == NodeKind.Event).ToList();
                var upcoming = events.Select(e => (Node: e, Next: e.Event!.NextOn(today)))
                                     .Where(x => x.Next is not null && x.Node.Event!.Status is not (EventStatus.Went or EventStatus.Missed))
                                     .OrderBy(x => x.Next).ToList();
                var undated = events.Where(e => e.Event!.Start is null).ToList();
                var past = events.Except(upcoming.Select(u => u.Node)).Except(undated)
                                 .OrderByDescending(e => e.Event!.Start).ToList();
                Section("Coming up", upcoming.Select(u => Line(u.Node, EventLine(u.Node, u.Next))));
                Section("No date yet", undated.Select(e => Line(e, EventLine(e, null))));
                Section("Past", past.Select(e => Line(e, EventLine(e, e.Event!.Start))));
                if (events.Count == 0) Empty($"No events here yet. Press {addText.TrimEnd('…')} to plan one — a race, a concert, a yoga class, a trip.");
                break;
            case SocialTree.MyThingsId:
                var things = group.SelfAndDescendants().Where(n => n.Kind == NodeKind.Thing).ToList();
                foreach (var g in things.GroupBy(t => t.Parent!).OrderBy(g => g.Key == group ? 0 : 1))
                    Section(g.Key == group ? "" : MainWindow.TopicPath(g.Key).Replace("My Things ▸ ", "", StringComparison.Ordinal), g.Select(t => Line(t, ThingLine(t))));
                if (things.Count == 0) Empty($"No things here yet. Press {addText.TrimEnd('…')} to keep track of your car, your house, a coffee maker — anything you own.");
                break;
            default:
                var people = group.SelfAndDescendants().Where(n => n.Kind == NodeKind.Person).ToList();
                Section("", people.Select(p => Line(p, PersonLine(p, today))));
                if (people.Count == 0) Empty("No one here yet.");
                break;
        }
    }

    private void Empty(string text) => _body.Children.Add(Ui.Text(text, hint: true, top: 8));

    private void Section(string title, IEnumerable<UIElement> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) return;
        if (title.Length > 0)
            _body.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 6) });
        foreach (var r in list) _body.Children.Add(r);
    }

    private Button Line(TreeNode n, string detail)
    {
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = n.Name, FontWeight = FontWeights.SemiBold });
        if (detail.Length > 0)
            text.Children.Add(new TextBlock { Text = detail, Foreground = (Brush)Application.Current.FindResource("Muted"), TextWrapping = TextWrapping.Wrap });
        var b = new Button
        {
            Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 0, 6), Background = Brushes.White, BorderBrush = (Brush)Application.Current.FindResource("Line"),
            MinWidth = 520,
        };
        AutomationProperties.SetName(b, detail.Length > 0 ? $"{n.Name}, {detail}" : n.Name);
        b.Click += (_, _) => OpenNode?.Invoke(n);
        return b;
    }

    internal static string EventLine(TreeNode n, DateTime? at)
    {
        var e = n.Event!;
        var parts = new List<string>();
        if (at is { } t) parts.Add(e.AllDay ? t.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture) : t.ToString("ddd MMM d, yyyy · h:mm tt", CultureInfo.CurrentCulture));
        if (e.Repeat != Repeat.None) parts.Add(e.Repeat switch { Repeat.Weekly => "every week", Repeat.Monthly => "every month", _ => "every year" });
        if (!string.IsNullOrWhiteSpace(e.Location)) parts.Add(e.Location!);
        if (e.People.Count > 0) parts.Add("with " + string.Join(", ", e.People.Select(p => p.Name)));
        if (n.TypeName is { } k) parts.Add(k);
        return string.Join("  ·  ", parts);
    }

    /// <summary>"Car · 2019 · Toyota · Camry" — the kind and the first few filled-in details.</summary>
    internal static string ThingLine(TreeNode n) =>
        string.Join("  ·  ", new[] { n.TypeName }.Concat(n.Details.Where(d => d.Value.Length > 0 && d.Kind != DetailKind.Note).Take(3).Select(d => d.Value)).OfType<string>());

    private static string PersonLine(TreeNode p, DateOnly today)
    {
        var parts = new List<string>();
        if (p.Details.FirstOrDefault(d => d.Kind == DetailKind.Birthday)?.AsDate() is { } b)
            parts.Add("birthday " + Dates.NextAnniversary(b, today).ToString("MMM d", CultureInfo.CurrentCulture));
        parts.AddRange(p.Children.Where(c => c.Account is not null).Select(c => c.Account!.Info.Name));
        return string.Join("  ·  ", parts);
    }
}
