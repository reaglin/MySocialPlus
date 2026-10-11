using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MySocialPlus.App.ViewModels;
using MySocialPlus.App.Windows;
using MySocialPlus.Core.Content;
using MySocialPlus.Core.Model;
using MySocialPlus.Core.Sources.Bluesky;
using MySocialPlus.Core.Sync;

namespace MySocialPlus.App;

/// <summary>A line above the content: something missing or wrong, and what to do about it.</summary>
public sealed record Notice(string Text, string? ActionText = null, Action? Act = null, bool Info = false);

/// <summary>
/// The tree on the left, the content of the selected node on the right (CLAUDE.md). Opening a node
/// lists everything under it in time order and marks it seen; a timer reads the networks every
/// 15 minutes while the window is open.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSession _s;
    private readonly ObservableCollection<NodeVm> _roots = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private NodeVm? _current;
    private bool _syncing;
    private bool _settingSort;
    private bool _showWelcome;
    private DateTimeOffset? _lastRound;

    public MainWindow(AppSession session)
    {
        _s = session;
        InitializeComponent();
        Title = _s.ReadsNetworks ? "MySocial+" : "MySocial+ — sample data";
        Tree.ItemsSource = _roots;
        NodeVm.ExpandedChanged += (_, _) => SaveTree();

        _details = new Views.DetailsView(_s);
        _details.Changed += _ => { _saveSoon.Stop(); _saveSoon.Start(); };
        _details.OpenNode += n => SelectNode(n, about: true);
        _details.AddProfile += p => SelectAndAddAccount(p);
        _details.OpenProfile += (a, who) =>
        {
            if (a.ProfileUrl is not { } url) return;
            if (a.IsReadable) OpenInBrowser(url); else OpenProfile($"{a.Info.Name} — {who}", url);
        };
        _group = new Views.GroupView();
        _group.OpenNode += n => SelectNode(n, about: true);
        _group.AddHere += g => AddTo(g);
        _group.NewTopicHere += g => NewTopicIn(g);
        _saveSoon.Tick += (_, _) =>
        {
            _saveSoon.Stop();
            SaveTree();
            // A name or date typed on the page shows in the tree, and a friend added from an event appears there.
            if (_s.Tree.AllNodes().Count() != AllVms().Count()) RebuildTree(_current?.Node.Id);
            foreach (var v in AllVms()) v.Refresh();
            if (_current is not null && _details.Node == _current.Node)
            {
                ContentTitle.Text = _current.Name;
                ContentSubtitle.Text = Subtitle(_current.Node, DateTimeOffset.Now);
            }
        };

        // Opens on What's new with nothing selected: selecting a node would mark it seen before Ron looked.
        RebuildTree();
        _showWelcome = IsEmpty();
        ShowNode(_current);

        _timer.Tick += async (_, _) => await SyncAsync(force: false);
        _seenTimer.Tick += SeenTimer_Tick;
        Loaded += async (_, _) =>
        {
            Tree.Focus();
            if (_s.ReadsNetworks) { _timer.Start(); await SyncAsync(force: false); }
        };
        // The last thing typed on a page is saved on closing: it may still be in a box, or waiting on the save timer.
        Closing += (_, _) => { System.Windows.Input.Keyboard.ClearFocus(); _saveSoon.Stop(); SaveTree(); };
        Closed += (_, _) => { _timer.Stop(); _seenTimer.Stop(); _s.Dispose(); };
    }

    private readonly Views.DetailsView _details;
    private readonly Views.GroupView _group;
    /// <summary>Saves a moment after the last keystroke on a page, not on every one.</summary>
    private readonly DispatcherTimer _saveSoon = new() { Interval = TimeSpan.FromMilliseconds(700) };

    /// <summary>Nothing in the tree yet: no accounts, people, events or things — show Getting started.</summary>
    private bool IsEmpty() => !_s.Tree.AllNodes().Any(n => n.Kind is NodeKind.Account or NodeKind.Person or NodeKind.Event or NodeKind.Thing);

    // ---- the tree ------------------------------------------------------------------------------

    private void RebuildTree(string? select = null)
    {
        select ??= _current?.Node.Id;
        _roots.Clear();
        foreach (var r in _s.Tree.Roots) _roots.Add(new NodeVm(r, null));
        RefreshBadges();
        var target = select is null ? null : AllVms().FirstOrDefault(v => v.Node.Id == select);
        _current = target;
        if (target is null) return;
        for (var p = target.Parent; p is not null; p = p.Parent) p.IsExpanded = true;
        target.IsSelected = true;
    }

    private IEnumerable<NodeVm> AllVms() => _roots.SelectMany(r => r.SelfAndDescendants());

    private void RefreshBadges()
    {
        var badges = Badges.Compute(_s.Tree, _s.Cache.UnseenByAccount());
        foreach (var v in AllVms())
        {
            v.Badge = badges.TryGetValue(v.Node.Id, out var b) ? b : 0;
            v.Refresh();
        }
        var total = _roots.Sum(r => r.Badge);
        Title = (total > 0 ? $"({total}) " : "") + (_s.ReadsNetworks ? "MySocial+" : "MySocial+ — sample data");
    }

    private void SaveTree()
    {
        if (!_s.ReadsNetworks) return;   // the sample is thrown away
        try { _s.Save(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            SetStatus($"Could not save your tree: {e.Message}");
        }
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is NodeVm v)
        {
            if (!ReferenceEquals(_current, v)) _keepNewFor = null;
            _current = v;
            _showWelcome = false;
            ShowNode(v);
        }
    }

    // ---- the content pane ----------------------------------------------------------------------

    private void ShowNode(NodeVm? v)
    {
        Welcome.Visibility = _showWelcome ? Visibility.Visible : Visibility.Collapsed;
        ContentArea.Visibility = _showWelcome ? Visibility.Collapsed : Visibility.Visible;
        UpdateWelcome();
        if (_showWelcome) return;
        if (v is null) { ShowOverview(); return; }

        var node = v.Node;
        var now = DateTimeOffset.Now;
        ContentTitle.Text = v.Name;
        ContentSubtitle.Text = Subtitle(node, now);
        SortPanel.Visibility = Visibility.Visible;
        Overview.Visibility = Visibility.Collapsed;

        _settingSort = true;
        SortBox.SelectedIndex = node.Sort == SortOrder.OldestUnreadFirst ? 1 : 0;
        _settingSort = false;

        Notices.ItemsSource = NoticesFor(node);
        UpdateAddButton();

        // Which page: a person, event or thing's details; a list of what is in a topic; or posts.
        var keys = node.AccountKeys();
        var page = PageFor(node, keys.Count);
        PersonTabs.Visibility = node.Kind == NodeKind.Person && keys.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (node.Kind == NodeKind.Person)
        {
            AboutTab.Content = $"About {node.Name}";
            _settingTab = true;
            (page == Page.Details ? AboutTab : PostsTab).IsChecked = true;
            _settingTab = false;
        }
        if (page != Page.Posts)
        {
            _seenTimer.Stop();
            _shownNodeId = node.Id;
            ThreadList.ItemsSource = null;
            ThreadList.Visibility = Visibility.Collapsed;
            SortPanel.Visibility = Visibility.Collapsed;
            EmptyText.Text = "";
            if (page == Page.Details) { _details.Show(node); PageHost.Content = _details; }
            else { _group.Show(node); PageHost.Content = _group; }
            PageHost.Visibility = Visibility.Visible;
            return;
        }
        PageHost.Visibility = Visibility.Collapsed;

        var items = _s.Cache.ForAccounts(keys);

        // Showing the same node again (a new order, new posts arriving) keeps marking what was new when
        // Ron opened it; opening a different node starts afresh.
        if (_shownNodeId != node.Id) { _shownNodeId = node.Id; _shownNew.Clear(); }
        foreach (var i in items)
        {
            var key = i.AccountKey + "|" + i.Id;
            if (!i.Seen) _shownNew.Add(key);
            else if (_shownNew.Contains(key)) i.Seen = false;
        }

        var threads = Threads.Build(items, node.Sort ?? SortOrder.NewestFirst).Select(t => new ThreadVm(t, now)).ToList();
        ThreadList.ItemsSource = threads;
        if (threads.Count > 0) ThreadList.ScrollIntoView(threads[0]);
        ThreadList.Visibility = threads.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SortPanel.Visibility = threads.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = threads.Count > 0 ? "" : EmptyMessage(node, keys.Count);

        // Opening a node marks what is in it as seen (rule 3) — once Ron has stayed on it a moment, so
        // moving through the tree with the arrow keys does not clear every badge on the way.
        _seenTimer.Stop();
        if (keys.Count > 0 && _keepNewFor != node.Id && _s.Cache.UnseenByAccount().Keys.Any(keys.Contains)) _seenTimer.Start();
    }

    private string? _shownNodeId;
    private string? _keepNewFor;
    private readonly HashSet<string> _shownNew = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _seenTimer = new() { Interval = TimeSpan.FromSeconds(1.2) };

    /// <summary>Ron has stayed on the node: mark it seen, and offer to take that back.</summary>
    private void SeenTimer_Tick(object? sender, EventArgs e)
    {
        _seenTimer.Stop();
        if (_current is null || _showWelcome || _current.Node.Id != _shownNodeId) return;
        var marked = _s.Cache.MarkSeen(_current.Node.AccountKeys());
        if (marked == 0) return;
        RefreshBadges();
        var node = _current;
        var list = NoticesFor(node.Node);
        list.Add(new Notice($"Marked {Plural(marked, "item")} here as seen.", "Mark as new again", () =>
        {
            _s.Cache.MarkUnseen(_shownNew.Select(k => { var bar = k.IndexOf('|', StringComparison.Ordinal); return (k[..bar], k[(bar + 1)..]); }));
            _keepNewFor = node.Node.Id;   // until Ron opens it again
            RefreshBadges();
            Notices.ItemsSource = NoticesFor(node.Node);
        }));
        Notices.ItemsSource = list;
    }

    private enum Page { Posts, Details, Group }

    private bool _settingTab;
    /// <summary>For a person with a readable account: Ron's last choice of tab (posts first until he picks About).</summary>
    private bool _personShowsAbout;

    private Page PageFor(TreeNode node, int readable) => node.Kind switch
    {
        NodeKind.Event or NodeKind.Thing => Page.Details,
        NodeKind.Person => readable == 0 || _personShowsAbout ? Page.Details : Page.Posts,
        NodeKind.Topic or NodeKind.Root when node.Root.Id is SocialTree.MyEventsId or SocialTree.MyThingsId => Page.Group,
        NodeKind.Topic or NodeKind.Root when node.Root.Id == SocialTree.MyFriendsId && readable == 0 => Page.Group,
        _ => Page.Posts,
    };

    private void PersonTab_Checked(object sender, RoutedEventArgs e)
    {
        if (_settingTab || _current is null) return;
        _personShowsAbout = AboutTab.IsChecked == true;
        ShowNode(_current);
    }

    /// <summary>What's new: what is coming up, and every person and account with something new. Nothing is marked seen.</summary>
    private void ShowOverview()
    {
        _shownNodeId = null;
        _seenTimer.Stop();
        PersonTabs.Visibility = Visibility.Collapsed;
        PageHost.Visibility = Visibility.Collapsed;
        UpdateAddButton();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var coming = ComingUp.Build(_s.Tree, today).Select(i => new ComingUpRow(i, today)).ToList();
        var rows = AllVms().Where(v => v.Badge > 0 && (v.Node.Kind == NodeKind.Person || v.Node.Account is { IsMine: true })).ToList();
        ContentTitle.Text = "What's new";
        ContentSubtitle.Text = coming.Count + rows.Count > 0
            ? "What is coming up in the next 30 days, and what is new on social media since you last looked. Opening something here does not mark anything else seen."
            : "Nothing coming up in the next 30 days, and nothing new. Open anything in the tree.";
        SortPanel.Visibility = Visibility.Collapsed;
        var notices = ProblemNotices(_s.Tree.Accounts().ToList());
        if (!_s.ReadsNetworks) notices.Insert(0, new Notice(SampleText, Info: true));
        Notices.ItemsSource = notices;
        ThreadList.ItemsSource = null;
        ThreadList.Visibility = Visibility.Collapsed;
        ComingUpList.ItemsSource = coming;
        ComingUpHeading.Visibility = ComingUpList.Visibility = coming.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        OverviewList.ItemsSource = rows;
        NewPostsHeading.Visibility = OverviewList.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Overview.Visibility = coming.Count + rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = coming.Count + rows.Count > 0 ? "" :
            "Birthdays, events and dates you asked to be reminded of show here when they are near; so does anything new from people you follow. Help ▸ Getting started shows where to begin.";
    }

    /// <summary>A line under Coming up, ready for the template.</summary>
    public sealed class ComingUpRow
    {
        public ComingUpRow(ComingUpItem i, DateOnly today)
        {
            Item = i;
            When = ComingUp.When(i, today);
            WhenBrush = i.IsOverdue(today) ? Brushes.Firebrick : (Brush)Application.Current.FindResource("Accent");
            Glyph = i.Kind switch { ComingUpKind.Event => "", ComingUpKind.Birthday => "", _ => "" };
        }
        public ComingUpItem Item { get; }
        public string When { get; }
        public Brush WhenBrush { get; }
        public string Glyph { get; }
        public string Title => Item.Title;
        public string? Detail => Item.Detail;
        public string Spoken => $"{When}: {Title}" + (Detail is null ? "" : $", {Detail}");
    }

    private void ComingUp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ComingUpRow r) SelectNode(r.Item.Node, about: true);
    }

    /// <summary>Opens a node in the tree (from a link on a page): expands its parents and selects it.</summary>
    private void SelectNode(TreeNode node, bool about = false)
    {
        if (AllVms().FirstOrDefault(v => v.Node.Id == node.Id) is not { } v) return;
        if (about) _personShowsAbout = true;
        _showWelcome = false;
        for (var p = v.Parent; p is not null; p = p.Parent) p.IsExpanded = true;
        if (v.IsSelected) ShowNode(v); else v.IsSelected = true;
    }

    private void OverviewRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not NodeVm v) return;
        for (var p = v.Parent; p is not null; p = p.Parent) p.IsExpanded = true;
        v.IsSelected = true;   // the tree's selection change opens it
    }

    private void WhatsNew_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not null) _current.IsSelected = false;
        _current = null;
        _showWelcome = false;
        ShowNode(null);
    }

    private string Subtitle(TreeNode node, DateTimeOffset now)
    {
        string Checked(Account a) => a.LastChecked is { } t ? $"last checked {When.Ago(t, now)}" : "not read yet";
        switch (node.Kind)
        {
            case NodeKind.Root when node.Id == SocialTree.MyPostsId:
                return "Your own posts, with who replied to, liked or shared them. Your own posts are never counted as new.";
            case NodeKind.Root when node.Id == SocialTree.MyEventsId:
                return "Races, concerts, classes, trips — with who is going and the plan. Soonest first.";
            case NodeKind.Root when node.Id == SocialTree.MyThingsId:
                return "What you own and want to keep track of: details, reminders and history.";
            case NodeKind.Root:
                return "Everyone in My Friends. Open a topic or a person to see just them.";
            case NodeKind.Topic:
                var count = node.SelfAndDescendants().Count(n => n.Kind is NodeKind.Person or NodeKind.Event or NodeKind.Thing);
                var word = node.Root.Id switch { SocialTree.MyEventsId => "event", SocialTree.MyThingsId => "thing", _ => "person" };
                return count == 1 ? $"1 {word} in this topic." : $"{count} {(word == "person" ? "people" : word + "s")} in this topic.";
            case NodeKind.Event:
                return Views.GroupView.EventLine(node, node.Event?.Start) is { Length: > 0 } line ? line : "No date yet — set it below.";
            case NodeKind.Thing:
                return Views.GroupView.ThingLine(node) is { Length: > 0 } t ? t : "Fill in what you want to keep below.";
            case NodeKind.Person:
                var accounts = node.Children.Where(c => c.Account is not null).Select(c => c.Account!).ToList();
                return accounts.Count == 0 ? (node.TypeName is { } kind ? $"{kind}. No social media linked — that is fine. Use Add a social media profile… below." : "No social media linked — that is fine. Use Add a social media profile… below.")
                    : "On " + string.Join(", ", accounts.Select(a => a.IsReadable ? $"{a.Info.Name} (@{a.Handle}, {Checked(a)})" : $"{a.Info.Name} (profile only)")) + ".";
            default:
                var acc = node.Account!;
                return acc.IsReadable ? $"@{acc.Handle} on {acc.Info.Name} · {Checked(acc)}" : $"{acc.Handle} on {acc.Info.Name} · profile only";
        }
    }

    private const string SampleText = "This is sample data with made-up people. Nothing is read from the networks, and changes are not saved.";

    /// <summary>
    /// What went wrong the last time, said once for many accounts: "could not reach Bluesky" for all of
    /// them, or "sign in again" for Ron's own account, with the button that fixes it.
    /// </summary>
    private List<Notice> ProblemNotices(IReadOnlyCollection<Account> accounts)
    {
        var list = new List<Notice>();
        foreach (var a in accounts.Where(a => a.IsMine && a.LastProblem == nameof(Core.Sources.SourceProblem.SignInNeeded)))
        {
            var text = _s.Secrets.Has(BlueskySource.SecretName(a.Handle))
                ? $"Bluesky no longer accepts MySocial+'s app password for @{a.Handle}. It may have been removed in Bluesky. Make a new one and paste it in."
                : $"Sign in to Bluesky so MySocial+ can read the replies and likes on your posts (@{a.Handle}).";
            list.Add(new Notice(text, "Sign in again…", () => BlueskySignIn_Click(this, new RoutedEventArgs())));
        }
        var failing = accounts.Where(a => a.IsReadable && a.LastError is not null && a.LastProblem != nameof(Core.Sources.SourceProblem.SignInNeeded)).ToList();
        if (failing.Count == 0) return list;
        var next = failing.Select(a => _s.Sync.Schedule.NextFor(a.Key)).OfType<DateTimeOffset>().DefaultIfEmpty(DateTimeOffset.Now).Min();
        var when = $"MySocial+ will try again at {next.ToLocalTime():t}.";
        var readable = accounts.Count(a => a.IsReadable);
        var words = failing.Count == readable && failing.Count > 1
            ? $"None of your accounts could be read the last time: {failing[0].LastError} {when}"
            : failing.Count == 1
                ? $"{Name(failing[0])} could not be read the last time: {failing[0].LastError} {when}"
                : $"{failing.Count} accounts could not be read the last time ({string.Join(", ", failing.Take(3).Select(Name))}{(failing.Count > 3 ? ", …" : "")}): {failing[0].LastError} {when}";
        list.Add(new Notice(words, "Try now", () => CheckNow_Click(this, new RoutedEventArgs())));
        return list;

        string Name(Account a) =>
            _s.Tree.AllNodes().FirstOrDefault(n => ReferenceEquals(n.Account, a)) is { } n && SocialTree.PersonOf(n) is { } p
                ? $"{p.Name} ({a.Info.Name})" : $"Your {a.Info.Name} account";
    }

    private List<Notice> NoticesFor(TreeNode node)
    {
        var list = new List<Notice>();
        if (!_s.ReadsNetworks) list.Add(new Notice(SampleText, Info: true));
        list.AddRange(ProblemNotices(node.SelfAndDescendants().Where(n => n.Account is not null).Select(n => n.Account!).ToList()));

        // Only for a person or an account: on a topic, a line per Facebook friend would bury the posts.
        if (node.Kind is NodeKind.Person or NodeKind.Account || node.Id == SocialTree.MyPostsId)
        {
            foreach (var n in node.SelfAndDescendants().Where(n => n.Account is not null))
            {
                var a = n.Account!;
                var person = SocialTree.PersonOf(n);
                var who = person?.Name ?? "You";
                if (!a.IsReadable)
                {
                    var url = a.ProfileUrl;
                    var also = person is not null && person.Children.Any(c => c.Account is { IsReadable: true }) ? "also " : "";
                    var text = a.IsMine
                        ? $"Your {a.Info.Name} account: reading your own {a.Info.Name} posts is not built yet, so it has no number."
                        : $"{who} is {also}on {a.Info.Name}. {a.Info.WhyNot}";
                    // A friend's page already has the big Open profile button; the line only explains.
                    // An explanation, not a problem: grey, not yellow. Its button is left out where the page's big one is showing.
                    var bigButton = !a.IsMine && node.Kind == NodeKind.Person && PageFor(node, node.AccountKeys().Count) == Page.Details;
                    list.Add(url is null || bigButton ? new Notice(text, Info: true)
                        : new Notice(text, $"Open {a.Info.Name} profile", () => OpenProfile(a.Info.Name + " — " + (a.IsMine ? "you" : who), url), Info: true));
                }
            }
        }
        return list;
    }

    private static string EmptyMessage(TreeNode node, int readable)
    {
        if (node.Id == SocialTree.MyPostsId && node.Children.Count == 0)
            return "None of your own accounts are here yet. Use Accounts ▸ Bluesky sign-in… to add yours.";
        if (node.Id == SocialTree.MyFriendsId && node.Children.Count == 0)
            return "No one here yet. Use Accounts ▸ Choose people I follow on Bluesky…, or Add person below the tree.";
        if (node.Kind == NodeKind.Topic && !node.Children.Any())
            return "This topic is empty. Select it and press Add person below the tree, or drag people onto it.";
        if (node.Kind == NodeKind.Person && !node.Children.Any(c => c.Account is not null))
            return $"{node.Name} has no accounts yet. Right-click them in the tree ▸ Add an account to this person….";
        if (readable == 0 && node.Kind is NodeKind.Person or NodeKind.Account)
            return "MySocial+ cannot read any of these accounts, so there is nothing to list. Use the button above to open the profile.";
        if (readable == 0)
            return "Nothing here can be read yet.";
        if (node.SelfAndDescendants().Any(n => n.Account is { IsReadable: true, LastChecked: null }))
            return "Not read yet. MySocial+ reads new accounts within a minute; or press Check now at the bottom right.";
        return "Nothing to show yet.";
    }

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settingSort || _current is null) return;
        _current.Node.Sort = SortBox.SelectedIndex == 1 ? SortOrder.OldestUnreadFirst : null;
        SaveTree();
        ShowNode(_current);
    }

    private void Notice_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Notice { Act: { } act }) act();
    }

    private void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string url) OpenInBrowser(url);
    }

    internal static void OpenInBrowser(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme is not ("https" or "http")) return;
        Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
    }

    private void OpenProfile(string title, string url) =>
        new ProfileWindow(title, url, System.IO.Path.Combine(_s.Paths.SecretsFolder, "WebView2")) { Owner = this }.Show();

    private void UpdateWelcome()
    {
        var mine = _s.Tree.MyPosts.Children.Any(c => c.Account?.Network == Network.Bluesky);
        WelcomeConnect.Content = mine ? "Connected ✓ — change…" : "Connect Bluesky…";
        WelcomeImport.IsEnabled = mine;
        WelcomeImport.ToolTip = mine ? null : "Connect your Bluesky account first (step 1).";
        ToolTipService.SetShowOnDisabled(WelcomeImport, true);
    }

    // ---- reading the networks ------------------------------------------------------------------

    private async Task SyncAsync(bool force)
    {
        if (_syncing || !_s.ReadsNetworks) return;
        _syncing = true;
        try
        {
            if (force) _s.Sync.Schedule.MakeAllDue();
            var now = DateTimeOffset.Now;
            var readable = _s.Tree.Accounts().Count(_s.Sync.CanRead);
            if (readable == 0)
            {
                SetStatus("Nothing to read yet: connect Bluesky and add some people (Help ▸ Getting started).");
                return;
            }
            var anyDue = _s.Tree.Accounts().Any(a => _s.Sync.CanRead(a) && _s.Sync.Schedule.IsDue(a.Key, now));
            if (!anyDue) { UpdateNextCheck(); return; }

            SetStatus("Checking for new posts…");
            var before = _current is null ? 0 : _current.Badge;
            var report = await _s.Sync.RunDueAsync(now, CancellationToken.None);
            _lastRound = now;
            SaveTree();
            RefreshBadges();

            var msg = report.NewItems switch
            {
                0 => $"Checked {Plural(report.AccountsChecked, "account")} at {now:t}: nothing new.",
                var n => $"Checked {Plural(report.AccountsChecked, "account")} at {now:t}: {n} new.",
            };
            if (report.Problems.Count > 0)
                msg += $" {Plural(report.Problems.Count, "account")} could not be read — What's new says why.";
            SetStatus(msg);

            // New things arrived in the node Ron is looking at: offer them rather than jumping the list.
            if (_current is not null && _current.Badge > before && !_showWelcome)
            {
                var list = NoticesFor(_current.Node);
                list.Insert(0, new Notice($"{Plural(_current.Badge, "new item")} arrived while you were reading.", "Show them", () => ShowNode(_current)));
                Notices.ItemsSource = list;
            }
            else if (_current is null && !_showWelcome)
            {
                ShowOverview();
            }
            else if (_current is not null && !_showWelcome)
            {
                ContentSubtitle.Text = Subtitle(_current.Node, DateTimeOffset.Now);
                Notices.ItemsSource = NoticesFor(_current.Node);
            }
        }
        finally
        {
            _syncing = false;
            UpdateNextCheck();
        }
    }

    private void UpdateNextCheck()
    {
        var next = _s.Tree.Accounts().Where(_s.Sync.CanRead).Select(a => _s.Sync.Schedule.NextFor(a.Key)).OfType<DateTimeOffset>().DefaultIfEmpty().Min();
        NextCheckText.Text = !_s.ReadsNetworks || next == default ? ""
            : next <= DateTimeOffset.Now ? "Next check: now" : $"Next check: {next.ToLocalTime():t}";
    }

    private static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";

    private void SetStatus(string text) => StatusText.Text = text;

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        if (!_s.ReadsNetworks) { SetStatus("Sample data: nothing is read from the networks."); return; }
        await SyncAsync(force: true);
    }

    // ---- keys ----------------------------------------------------------------------------------

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5) { e.Handled = true; await SyncAsync(force: true); }
    }

    private void Tree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2) { Rename_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Delete) { Delete_Click(sender, e); e.Handled = true; }
    }

    // ---- editing the tree ----------------------------------------------------------------------

    /// <summary>The topic (or My Friends) a new person or topic goes into, from what is selected.</summary>
    /// <summary>The group (a topic or a heading) new things go into, under <paramref name="rootId"/>, from what is selected.</summary>
    private TreeNode TargetGroup(string rootId)
    {
        var root = _s.Tree.Roots.First(r => r.Id == rootId);
        for (var n = _current?.Node; n is not null; n = n.Parent)
            if (n.Root.Id == rootId && (n.Kind == NodeKind.Topic || n.Kind == NodeKind.Root)) return n;
        return root;
    }

    /// <summary>Where a new topic goes: the selected heading (My Friends when it is My Posts or nothing).</summary>
    private TreeNode TargetTopic()
    {
        var rootId = _current?.Node.Root.Id is { } r && r != SocialTree.MyPostsId ? r : SocialTree.MyFriendsId;
        return TargetGroup(rootId);
    }

    private void UpdateAddButton()
    {
        var root = _current?.Node.Root.Id;
        (AddButton.Content, AddButton.ToolTip) = root switch
        {
            SocialTree.MyEventsId => ("New event", "Plan an event in the selected topic: a race, a concert, a class, a trip."),
            SocialTree.MyThingsId => ("New thing", "Keep track of something you own in the selected topic: a car, a house, an appliance."),
            _ => ("Add person", "Add someone to the selected topic: their name, and their Facebook profile or other account if you like."),
        };
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => AddTo(_current?.Node.Root ?? _s.Tree.MyFriends);

    /// <summary>The add that fits a heading: a person, an event or a thing.</summary>
    private void AddTo(TreeNode group)
    {
        switch (group.Root.Id)
        {
            case SocialTree.MyEventsId: NewItem(TemplateFor.Event, group.Kind is NodeKind.Topic or NodeKind.Root ? group : TargetGroup(SocialTree.MyEventsId)); break;
            case SocialTree.MyThingsId: NewItem(TemplateFor.Thing, group.Kind is NodeKind.Topic or NodeKind.Root ? group : TargetGroup(SocialTree.MyThingsId)); break;
            default: AddPersonIn(group.Kind is NodeKind.Topic or NodeKind.Root && group.Root.Id == SocialTree.MyFriendsId ? group : TargetGroup(SocialTree.MyFriendsId)); break;
        }
    }

    private void NewEvent_Click(object sender, RoutedEventArgs e) => NewItem(TemplateFor.Event, TargetGroup(SocialTree.MyEventsId));

    private void NewThing_Click(object sender, RoutedEventArgs e) => NewItem(TemplateFor.Thing, TargetGroup(SocialTree.MyThingsId));

    private void NewItem(TemplateFor kind, TreeNode group)
    {
        var dlg = new NewItemWindow(_s.Templates, kind, group.Kind == NodeKind.Root ? group.Name : TopicPath(group)) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is not { } node) return;
        group.Expanded = true;
        Apply(() => _s.Tree.Add(group, node), node.Id);
    }


    private void NewTopic_Click(object sender, RoutedEventArgs e) => NewTopicIn(TargetTopic());

    private void NewTopicIn(TreeNode parent)
    {
        if (parent.Kind is not (NodeKind.Topic or NodeKind.Root) || parent.Id == SocialTree.MyPostsId) parent = TargetTopic();
        var where = parent.Kind == NodeKind.Root ? $"under {parent.Name}" : $"inside “{parent.Name}”";
        var example = parent.Root.Id switch
        {
            SocialTree.MyEventsId => "Running, Concerts",
            SocialTree.MyThingsId => "Vehicles, Kitchen",
            _ => "Family, Colleagues",
        };
        var name = TextPrompt.Ask(this, "New topic", $"Name the new topic (like {example}). It goes {where}; you can drag it somewhere else later.", "");
        if (name is null) return;
        var topic = TreeNode.Topic(name);
        Apply(() => _s.Tree.Add(parent, topic), topic.Id);
    }

    private void AddPerson_Click(object sender, RoutedEventArgs e) => AddPersonIn(TargetGroup(SocialTree.MyFriendsId));

    private void AddPersonIn(TreeNode group)
    {
        var dlg = new PersonWindow(_s.Tree, group, _s.Templates) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is not { } r) return;
        var person = TreeNode.Person(r.Name);
        r.Kind?.ApplyTo(person);
        _personShowsAbout = r.Account is not { IsReadable: true };
        Apply(() =>
        {
            _s.Tree.Add(r.Topic, person);
            if (r.Account is { } a) _s.Tree.Add(person, TreeNode.ForAccount(a));
        }, person.Id);
        if (r.Account is { IsReadable: true }) _ = SyncAsync(force: false);
    }

    private void Apply(Action change, string? select = null)
    {
        try
        {
            change();
            SaveTree();
            RebuildTree(select);
            _showWelcome = false;
            ShowNode(_current);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "MySocial+", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var person = _current is null ? null : SocialTree.PersonOf(_current.Node);
        if (person is null)
        {
            MessageBox.Show(this, "Select a person in the tree first (under My Friends), then add their account on another network.\n\n" +
                                  "To add your own Bluesky account, use Accounts ▸ Bluesky sign-in….",
                            "Add an account", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectAndAddAccount(person);
    }

    /// <summary>Links a friend's profile (Facebook or another network) to them.</summary>
    private void SelectAndAddAccount(TreeNode person)
    {
        var dlg = new PersonWindow(_s.Tree, person) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result?.Account is not { } a) return;
        var node = TreeNode.ForAccount(a);
        Apply(() => _s.Tree.Add(person, node), person.Id);
        if (a.IsReadable) _ = SyncAsync(force: false);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not { } v) return;
        if (v.Node.Kind is NodeKind.Root or NodeKind.Account)
        {
            MessageBox.Show(this, v.Node.Kind == NodeKind.Root ? "The four headings keep their names." : "An account is named by its network and handle. Rename the person instead.",
                            "Rename", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var name = TextPrompt.Ask(this, "Rename", $"New name for “{v.Node.Name}”:", v.Node.Name);
        if (name is not null) Apply(() => SocialTree.Rename(v.Node, name), v.Node.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not { } v) return;
        var n = v.Node;
        if (n.Kind == NodeKind.Root)
        {
            MessageBox.Show(this, "The four headings cannot be deleted.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var inside = n.SelfAndDescendants().Count(x => x.Kind is NodeKind.Person or NodeKind.Event or NodeKind.Thing);
        var what = n.Kind switch
        {
            NodeKind.Topic when inside > 0 => $"the topic “{n.Name}” and everything in it ({Plural(inside, "item")})",
            NodeKind.Topic => $"the topic “{n.Name}”",
            NodeKind.Person => $"{n.Name}, and everything you kept about them",
            NodeKind.Event => $"the event “{n.Name}”",
            NodeKind.Thing => $"“{n.Name}”, with its details and history",
            _ => $"the {n.Account!.Info.Name} account {n.Account.Handle}",
        };
        var mine = n.SelfAndDescendants().Where(x => x.Account is { IsMine: true, Network: Network.Bluesky }).Select(x => x.Account!).ToList();
        var ok = MessageBox.Show(this,
            $"Remove {what} from MySocial+?\n\nThis only changes your tree here. Nothing is unfollowed or changed on any network." +
            (mine.Count > 0 ? "\n\nMySocial+ will also forget the saved Bluesky app password." : ""),
            "Delete", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (ok != MessageBoxResult.OK) return;
        var keys = n.SelfAndDescendants().Where(x => x.Account is not null).Select(x => x.Account!.Key).ToList();
        Apply(() =>
        {
            _s.Tree.Delete(n);
            foreach (var k in keys) _s.Cache.Forget(k);
            foreach (var a in mine) { _s.Secrets.Delete(BlueskySource.SecretName(a.Handle)); _s.Bluesky.Client.SignOut(a.Handle); }
        }, n.Parent?.Id);
    }

    private void Tree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Right-click selects what is under the mouse first, as Explorer does.
        if (e.OriginalSource is DependencyObject d && FindParent<TreeViewItem>(d) is { DataContext: NodeVm v }) v.IsSelected = true;
        var menu = Tree.ContextMenu!;
        menu.Items.Clear();
        MenuItem Item(string header, RoutedEventHandler click, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            mi.Click += click;
            menu.Items.Add(mi);
            return mi;
        }
        var node = _current?.Node;
        switch (node?.Root.Id)
        {
            case null or SocialTree.MyFriendsId:
                Item("New topic…", NewTopic_Click);
                Item("Add a person…", AddPerson_Click);
                break;
            case SocialTree.MyEventsId:
                Item("New topic…", NewTopic_Click);
                Item("New event…", NewEvent_Click);
                break;
            case SocialTree.MyThingsId:
                Item("New topic…", NewTopic_Click);
                Item("New thing…", NewThing_Click);
                break;
        }
        if (node is not null && SocialTree.PersonOf(node) is not null) Item("Add an account to this person…", AddAccount_Click);
        if (node?.Account is { } a && a.ProfileUrl is { } url) Item(a.IsReadable ? $"Open on {a.Info.Name}" : "Open profile", (_, _) =>
        {
            if (a.IsReadable) OpenInBrowser(url); else OpenProfile($"{a.Info.Name} — {SocialTree.PersonOf(node)?.Name ?? "you"}", url);
        });
        if (node?.Kind is NodeKind.Person or NodeKind.Topic or NodeKind.Event or NodeKind.Thing)
        {
            var move = new MenuItem { Header = "Move to" };
            var home = node.Root;
            var targets = new[] { home }.Concat(home.SelfAndDescendants().Where(t => t.Kind == NodeKind.Topic))
                                                       .Where(t => !ReferenceEquals(t, node.Parent) && SocialTree.WhyNotAllowed(t, node) is null);
            foreach (var t in targets)
            {
                var mi = new MenuItem { Header = TopicPath(t) };
                mi.Click += (_, _) => Apply(() => _s.Tree.Move(node, t), node.Id);
                move.Items.Add(mi);
            }
            move.IsEnabled = move.Items.Count > 0;
            menu.Items.Add(move);
        }
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        Item("Rename…", Rename_Click, node?.Kind is NodeKind.Topic or NodeKind.Person or NodeKind.Event or NodeKind.Thing);
        Item("Delete", Delete_Click, node is not null && node.Kind != NodeKind.Root);
    }

    internal static string TopicPath(TreeNode t)
    {
        var parts = new List<string>();
        for (var n = t; n is not null; n = n.Parent) parts.Insert(0, n.Name);
        return string.Join(" ▸ ", parts);
    }

    // ---- drag a person or topic onto a topic ---------------------------------------------------

    private Point _dragStart;
    private NodeVm? _dragItem;

    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(Tree);
        _dragItem = e.OriginalSource is DependencyObject d && FindParent<TreeViewItem>(d) is { DataContext: NodeVm v } ? v : null;
    }

    private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null) return;
        if (_dragItem.Node.Kind is not (NodeKind.Person or NodeKind.Topic or NodeKind.Event or NodeKind.Thing)) return;
        var d = e.GetPosition(Tree) - _dragStart;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(Tree, item, DragDropEffects.Move);
    }

    /// <summary>Dropping on a topic puts it inside; dropping on a person puts it beside them.</summary>
    private (TreeNode Parent, string? Why)? DropTarget(DragEventArgs e)
    {
        if (e.Data.GetData(typeof(NodeVm)) is not NodeVm dragged) return null;
        if (e.OriginalSource is not DependencyObject d || FindParent<TreeViewItem>(d) is not { DataContext: NodeVm over }) return null;
        var parent = over.Node.Kind is NodeKind.Person or NodeKind.Event or NodeKind.Thing or NodeKind.Account ? (SocialTree.PersonOf(over.Node) ?? over.Node).Parent! : over.Node;
        if (ReferenceEquals(parent, dragged.Node.Parent)) return (parent, "It is already there.");
        return (parent, SocialTree.WhyNotAllowed(parent, dragged.Node));
    }

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        var t = DropTarget(e);
        e.Effects = t is { Why: null } ? DragDropEffects.Move : DragDropEffects.None;
        if (t is { } x) SetStatus(x.Why is null ? $"Drop to move into “{x.Parent.Name}”." : x.Why);
        e.Handled = true;
    }

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        if (DropTarget(e) is not { Why: null } t || e.Data.GetData(typeof(NodeVm)) is not NodeVm dragged) return;
        Apply(() => _s.Tree.Move(dragged.Node, t.Parent), dragged.Node.Id);
        SetStatus($"Moved {dragged.Node.Name} into “{t.Parent.Name}”.");
    }

    private static T? FindParent<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T) d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as T;
    }

    // ---- accounts ------------------------------------------------------------------------------

    private void BlueskySignIn_Click(object sender, RoutedEventArgs e)
    {
        if (!_s.ReadsNetworks)
        {
            MessageBox.Show(this, "This is the sample, so nothing connects to Bluesky. Start MySocial+ normally to connect your account.",
                            "Bluesky sign-in", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new BlueskySignInWindow(_s) { Owner = this };
        dlg.ShowDialog();
        if (dlg.Changed)
        {
            SaveTree();
            // Getting started stays up until people are in the tree: step 2 is next.
            var hasFriends = _s.Tree.MyFriends.SelfAndDescendants().Any(n => n.Account is not null);
            RebuildTree(hasFriends ? dlg.AccountNodeId ?? SocialTree.MyPostsId : null);
            _showWelcome = !hasFriends;
            ShowNode(_current);
            _ = SyncAsync(force: true);
        }
        if (dlg.WantsImport) ImportFollows_Click(sender, e);
    }

    private void ImportFollows_Click(object sender, RoutedEventArgs e)
    {
        var mine = _s.Tree.MyPosts.Children.Select(c => c.Account).FirstOrDefault(a => a?.Network == Network.Bluesky);
        if (mine is null || !_s.ReadsNetworks)
        {
            MessageBox.Show(this, _s.ReadsNetworks
                ? "Connect your own Bluesky account first (Accounts ▸ Bluesky sign-in…). MySocial+ then lists everyone you follow, and you pick who to bring in."
                : "This is the sample, so nothing is read from Bluesky.",
                "Choose people I follow", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new ImportFollowsWindow(_s, mine, TargetTopic()) { Owner = this };
        if (dlg.ShowDialog() != true)
        {
            // Topics made in the window stay, even when no one was added: show and save them.
            if (dlg.TopicsMade > 0) { SaveTree(); RebuildTree(_current?.Node.Id); ShowNode(_current); }
            return;
        }
        SaveTree();
        RebuildTree(dlg.TopicId);
        _showWelcome = false;
        ShowNode(_current);
        SetStatus($"Added {Plural(dlg.Added, "person")}. Their posts will appear after the next check, in a moment.");
        _ = SyncAsync(force: false);
    }

    // ---- other menus ---------------------------------------------------------------------------

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(_s.Paths.DataFolder) { UseShellExecute = true });

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void GettingStarted_Click(object sender, RoutedEventArgs e)
    {
        _showWelcome = true;
        ShowNode(_current);
    }

    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private void AiSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hub = Eaglin.AiManager.AiHub.Open("MySocialPlus");
            new Eaglin.AiManager.Wpf.AiSettingsWindow(hub) { Owner = this }.ShowDialog();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, "AI Settings could not open: " + ex.Message, "AI Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
