using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MySocialPlus.Core.Model;

namespace MySocialPlus.App.Windows;

/// <summary>
/// Adds a person (name, topic, and their first account), or — opened on a person — adds another
/// account to them. Says for each network what MySocial+ can read there, before Ron chooses.
/// </summary>
public sealed class PersonWindow : Window
{
    public sealed record Answer(string Name, TreeNode Topic, Account? Account, Template? Kind);

    private readonly SocialTree _tree;
    private readonly TreeNode? _person;
    private readonly TextBox _name;
    private readonly ComboBox _topic;
    private readonly ComboBox _kind = new() { DisplayMemberPath = "Name" };
    private readonly ComboBox _network;
    private readonly TextBox _handle;
    private readonly TextBlock _networkHint;
    private readonly TextBlock _handleHint;
    private readonly TextBlock _error;

    public Answer? Result { get; private set; }

    /// <param name="at">A topic (or My Friends) to add a new person to; or a person to add an account to.</param>
    public PersonWindow(SocialTree tree, TreeNode at, TemplateSet? templates = null)
    {
        _tree = tree;
        AutomationProperties.SetName(_kind, "Kind of person");
        foreach (var k in templates?.For(TemplateFor.Person) ?? []) _kind.Items.Add(k);
        _kind.SelectedIndex = _kind.Items.Count > 0 ? 0 : -1;
        _person = at.Kind == NodeKind.Person ? at : null;
        Title = _person is null ? "Add a person" : $"Add an account for {_person.Name}";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource("AppWindow");

        _name = Ui.TextBox("Their name");
        _topic = new ComboBox { DisplayMemberPath = "Item2" };
        AutomationProperties.SetName(_topic, "Topic");
        foreach (var t in new[] { tree.MyFriends }.Concat(tree.MyFriends.SelfAndDescendants().Where(n => n.Kind == NodeKind.Topic)))
            _topic.Items.Add(Tuple.Create(t, t.Id == SocialTree.MyFriendsId ? "My Friends (no topic)" : MainWindow.TopicPath(t)));
        _topic.SelectedIndex = Math.Max(0, _topic.Items.Cast<Tuple<TreeNode, string>>().ToList().FindIndex(x => ReferenceEquals(x.Item1, at)));

        _network = new ComboBox { DisplayMemberPath = "Name" };
        AutomationProperties.SetName(_network, "Network");
        foreach (var n in new[] { Network.Facebook, Network.Bluesky, Network.Instagram, Network.X, Network.Threads, Network.LinkedIn, Network.YouTube, Network.Reddit, Network.Mastodon })
            _network.Items.Add(NetworkInfo.For(n));
        _network.SelectedIndex = 0;
        _network.SelectionChanged += (_, _) => UpdateHints();

        _handle = Ui.TextBox("Their handle or profile address");
        _networkHint = Ui.Text("", hint: true, top: 4);
        _handleHint = Ui.Text("", hint: true, top: 4);
        _error = Ui.Text("", top: 8);
        _error.Foreground = System.Windows.Media.Brushes.Firebrick;

        var stack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        if (_person is null)
        {
            stack.Children.Add(Ui.Text("Add someone to your tree. You can give them more accounts later (right-click them ▸ Add an account)."));
            stack.Children.Add(Ui.Label("Name"));
            stack.Children.Add(_name);
            stack.Children.Add(Ui.Label("Topic"));
            stack.Children.Add(_topic);
            if (_kind.Items.Count > 0)
            {
                stack.Children.Add(Ui.Label("Kind"));
                stack.Children.Add(_kind);
                stack.Children.Add(Ui.Text("Adds the details usually kept: birthday, children, likes, phone, email, a Google Photos link… all optional.", hint: true, top: 4));
            }
        }
        else
        {
            stack.Children.Add(Ui.Text($"Add {_person.Name}'s account on another network."));
        }
        stack.Children.Add(Ui.Label("Network"));
        stack.Children.Add(_network);
        stack.Children.Add(_networkHint);
        stack.Children.Add(Ui.Label(_person is null ? "Their profile address or handle (you can leave it empty)" : "Their profile address or handle"));
        stack.Children.Add(_handle);
        stack.Children.Add(_handleHint);
        stack.Children.Add(_error);
        stack.Children.Add(Ui.Buttons(
            Ui.Button(_person is null ? "Add person" : "Add account", (_, _) => Ok(), primary: true, isDefault: true),
            Ui.Button("Cancel", (_, _) => DialogResult = false, isCancel: true)));
        Content = stack;
        UpdateHints();
        Loaded += (_, _) => (_person is null ? _name : (Control)_network).Focus();
    }

    private NetworkInfo Info => (NetworkInfo)_network.SelectedItem;

    private void UpdateHints()
    {
        var info = Info;
        _networkHint.Text = info.CanReadFriends
            ? $"MySocial+ reads their {info.Name} posts and shows a number when there is something new."
            : info.WhyNot + " Their node opens their profile inside MySocial+ instead.";
        _handleHint.Text = info.Network switch
        {
            Network.Bluesky => "Like sarahlee.bsky.social — it is under their name on their Bluesky profile.",
            Network.Facebook => "Open their profile on Facebook and copy the address from the browser, like www.facebook.com/jane.sample.42 — paste it here.",
            Network.Mastodon or Network.Rss => "Paste the full address of their profile, like https://mastodon.social/@name.",
            _ => "Their user name on " + info.Name + ", or paste the address of their profile page.",
        };
    }

    private void Ok()
    {
        _error.Text = "";
        var name = _name.Text.Trim();
        var handle = _handle.Text.Trim();
        if (_person is null && name.Length == 0) { _error.Text = "Type their name."; _name.Focus(); return; }
        if (_person is not null && handle.Length == 0) { _error.Text = "Type their handle, or paste their profile address."; _handle.Focus(); return; }

        Account? account = null;
        if (handle.Length > 0)
        {
            var net = Info.Network;
            // A pasted profile address (www.facebook.com/jane.sample.42, bsky.app/profile/…) becomes the handle.
            handle = Info.NormalizeHandle(handle);
            if (net == Network.Bluesky)
            {
                if (!handle.Contains('.', StringComparison.Ordinal))
                {
                    _error.Text = "A Bluesky handle has a dot in it, like sarahlee.bsky.social. Check it on their profile.";
                    return;
                }
            }
            // Without a profile address pattern (Mastodon lives on many servers), only a full address can be opened.
            if (Info.ProfileUrlFormat is null && !handle.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _error.Text = "Paste the full address of their profile, like https://mastodon.social/@name.";
                return;
            }
            account = new Account { Network = net, Handle = handle };
            if (_tree.Accounts().Any(a => a.Key == account.Key))
            {
                _error.Text = $"{handle} on {Info.Name} is already in your tree.";
                return;
            }
        }
        var topic = _person?.Parent ?? ((Tuple<TreeNode, string>)_topic.SelectedItem).Item1;
        Result = new Answer(_person?.Name ?? name, topic, account, _kind.SelectedItem as Template);
        DialogResult = true;
    }
}
