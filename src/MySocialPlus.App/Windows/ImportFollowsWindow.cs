using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MySocialPlus.Core.Model;
using MySocialPlus.Core.Sources;
using MySocialPlus.Core.Sources.Bluesky;

namespace MySocialPlus.App.Windows;

/// <summary>
/// Lists everyone Ron follows on Bluesky; he ticks the ones he wants and picks the topic they go
/// in (plan 3.3). People already in the tree are shown but cannot be added twice.
/// </summary>
public sealed class ImportFollowsWindow : Window
{
    private readonly AppSession _s;
    private readonly Account _mine;
    private readonly TextBox _search;
    private readonly StackPanel _list = new();
    private readonly ComboBox _topic = new() { DisplayMemberPath = "Item2", MinWidth = 220 };
    private readonly TextBlock _status;
    private readonly Button _add;
    private readonly List<(BlueskyActor Who, CheckBox Box)> _rows = [];

    public int Added { get; private set; }
    public string? TopicId { get; private set; }

    public ImportFollowsWindow(AppSession session, Account mine, TreeNode suggestedTopic)
    {
        _s = session;
        _mine = mine;
        Title = "Choose people you follow on Bluesky";
        Width = 640; Height = 720; MinWidth = 520; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource("AppWindow");

        _search = Ui.TextBox("Find a person");
        _search.TextChanged += (_, _) => Filter();
        AutomationProperties.SetName(_topic, "Put them in this topic");
        FillTopics(suggestedTopic);
        _status = Ui.Text("Loading the people you follow…", hint: true);
        _add = Ui.Button("Add", (_, _) => Add(), primary: true, isDefault: true);
        _add.IsEnabled = false;

        var top = Ui.Stack(
            Ui.Text($"Tick the people you want in MySocial+. These are everyone @{mine.Handle} follows on Bluesky. " +
                    "Adding someone here does not change anything on Bluesky."),
            Ui.Label("Find", 12), _search);
        top.Margin = new Thickness(20, 16, 20, 8);

        var pick = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        pick.Children.Add(new Button { Content = "Tick all shown", Margin = new Thickness(0, 0, 8, 0) });
        pick.Children.Add(new Button { Content = "Untick all", Margin = new Thickness(0, 0, 8, 0) });
        ((Button)pick.Children[0]).Click += (_, _) => { foreach (var r in _rows) if (r.Box.Visibility == Visibility.Visible && r.Box.IsEnabled) r.Box.IsChecked = true; Count(); };
        ((Button)pick.Children[1]).Click += (_, _) => { foreach (var r in _rows) if (r.Box.IsEnabled) r.Box.IsChecked = false; Count(); };
        top.Children.Add(pick);

        var topicRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var newTopic = Ui.Button("New topic…", (_, _) =>
        {
            var name = TextPrompt.Ask(this, "New topic", "Name the new topic. It goes under My Friends.", "");
            if (name is null) return;
            var t = TreeNode.Topic(name);
            _s.Tree.Add(_s.Tree.MyFriends, t);
            FillTopics(t);
        });
        DockPanel.SetDock(newTopic, Dock.Right);
        topicRow.Children.Add(newTopic);
        topicRow.Children.Add(_topic);

        var bottom = Ui.Stack(_status, Ui.Label("Put the people you tick in", 10), topicRow,
            Ui.Buttons(_add, Ui.Button("Cancel", (_, _) => DialogResult = false, isCancel: true)));
        bottom.Margin = new Thickness(20, 8, 20, 16);

        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(top);
        dock.Children.Add(bottom);
        var scroll = new ScrollViewer { Content = _list, Margin = new Thickness(20, 0, 20, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        AutomationProperties.SetName(scroll, "People you follow");
        dock.Children.Add(scroll);
        Content = dock;

        Loaded += async (_, _) => await LoadAsync();
    }

    private void FillTopics(TreeNode select)
    {
        _topic.Items.Clear();
        foreach (var t in new[] { _s.Tree.MyFriends }.Concat(_s.Tree.MyFriends.SelfAndDescendants().Where(n => n.Kind == NodeKind.Topic)))
        {
            _topic.Items.Add(Tuple.Create(t, t.Id == SocialTree.MyFriendsId ? "My Friends (no topic)" : MainWindow.TopicPath(t)));
            if (ReferenceEquals(t, select)) _topic.SelectedIndex = _topic.Items.Count - 1;
        }
        if (_topic.SelectedIndex < 0) _topic.SelectedIndex = 0;
    }

    private async Task LoadAsync()
    {
        List<BlueskyActor> people;
        try { people = await _s.Bluesky.Client.GetAllFollowsAsync(_mine.Id ?? _mine.Handle, CancellationToken.None); }
        catch (SourceException e) { _status.Text = e.Message; return; }

        var inTree = _s.Tree.Accounts().Where(a => a.Network == Network.Bluesky).SelectMany(a => new[] { a.Id, a.Handle }).OfType<string>()
                       .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in people.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var already = inTree.Contains(p.Did) || inTree.Contains(p.Handle);
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = p.Name, FontWeight = FontWeights.SemiBold });
            label.Children.Add(new TextBlock
            {
                Text = "@" + p.Handle + (already ? " · already in your tree" : "") +
                       (string.IsNullOrWhiteSpace(p.Description) ? "" : " · " + Shorten(p.Description!.ReplaceLineEndings(" "), 90)),
                Style = (Style)Application.Current.FindResource("Hint"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var box = new CheckBox { Content = label, Margin = new Thickness(0, 4, 0, 4), IsEnabled = !already, IsChecked = already ? true : false };
            AutomationProperties.SetName(box, already ? $"{p.Name}, already in your tree" : p.Name);
            box.Checked += (_, _) => Count();
            box.Unchecked += (_, _) => Count();
            _rows.Add((p, box));
            _list.Children.Add(box);
        }
        Count();
        if (people.Count == 0) _status.Text = $"@{_mine.Handle} does not follow anyone on Bluesky yet. Follow people there, then come back.";
        _search.Focus();
    }

    private void Filter()
    {
        var q = _search.Text.Trim();
        foreach (var (who, box) in _rows)
            box.Visibility = q.Length == 0 || who.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || who.Handle.Contains(q, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Count()
    {
        var n = _rows.Count(r => r.Box.IsEnabled && r.Box.IsChecked == true);
        var total = _rows.Count;
        _status.Text = $"You follow {total} {(total == 1 ? "person" : "people")} on Bluesky. {n} ticked to add.";
        _add.Content = n == 1 ? "Add 1 person" : $"Add {n} people";
        _add.IsEnabled = n > 0;
    }

    private void Add()
    {
        var topic = ((Tuple<TreeNode, string>)_topic.SelectedItem).Item1;
        foreach (var (who, box) in _rows.Where(r => r.Box.IsEnabled && r.Box.IsChecked == true))
        {
            var person = TreeNode.Person(who.Name);
            _s.Tree.Add(topic, person);
            _s.Tree.Add(person, TreeNode.ForAccount(new Account
            {
                Network = Network.Bluesky, Handle = who.Handle, Id = who.Did, DisplayName = who.DisplayName, AvatarUrl = who.AvatarUrl,
            }));
            Added++;
        }
        topic.Expanded = true;
        TopicId = topic.Id;
        DialogResult = true;
    }

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
