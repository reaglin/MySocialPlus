using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MySocialPlus.App.Windows;
using MySocialPlus.Core.Content;
using MySocialPlus.Core.Model;

namespace MySocialPlus.App.Views;

/// <summary>
/// Everything Ron keeps about one person, event or thing: its name and kind, an event's when / where /
/// team / agenda, the name/value details (any number, any names), and the dated log. Saved as he
/// types (the window saves a moment later). Built in code because its rows come from the data.
/// </summary>
public sealed class DetailsView : UserControl
{
    private readonly AppSession _s;
    private readonly StackPanel _body = new() { Margin = new Thickness(20, 12, 20, 24), MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left };
    private TreeNode? _node;
    private WrapPanel? _linkHost;
    /// <summary>Empty suggested details are folded away until Ron asks to see them.</summary>
    private bool _showEmpty;

    /// <summary>Something changed: save soon, and refresh the tree's label.</summary>
    public event Action<TreeNode>? Changed;
    /// <summary>Ron clicked a person or event named here: open it in the tree.</summary>
    public event Action<TreeNode>? OpenNode;
    /// <summary>Open a person's profile on a network (the window shows it inside the program).</summary>
    public event Action<Account, string>? OpenProfile;
    /// <summary>"Add a social media profile…" pressed on a person's page.</summary>
    public event Action<TreeNode>? AddProfile;

    public DetailsView(AppSession session)
    {
        _s = session;
        Content = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public TreeNode? Node => _node;

    public void Show(TreeNode node)
    {
        if (!ReferenceEquals(_node, node)) _showEmpty = false;
        _node = node;
        Render();
    }

    private void Touch() { if (_node is not null) Changed?.Invoke(_node); }

    private TemplateFor For => _node!.Kind switch { NodeKind.Event => TemplateFor.Event, NodeKind.Thing => TemplateFor.Thing, _ => TemplateFor.Person };

    private Template? CurrentTemplate => _s.Templates.Find(_node!.TypeName, For);

    // ---- building the page ---------------------------------------------------------------------

    private void Render()
    {
        _body.Children.Clear();
        var n = _node!;
        _linkHost = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        _body.Children.Add(_linkHost);
        LinkBar(n);
        NameAndKind(n);
        if (n.Event is not null) EventPanel(n, n.Event);
        if (n.Event is not null) Team(n);
        if (n.Event is not null) Agenda(n.Event);
        // A thing's history is what Ron adds to most (a service, a repair), so it comes before its long list of details.
        if (n.Kind == NodeKind.Thing) { Log(n); DetailRows(n); }
        else { DetailRows(n); Log(n); }
        if (n.Kind == NodeKind.Person) EventsWith(n);
        Footer(n);
    }

    private static TextBlock Heading(string text, double top = 22) => new()
    {
        Text = text, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 6),
    };

    private static TextBlock Hint(string text, double top = 2) => Ui.Text(text, hint: true, top: top);

    private static Button Small(string text, RoutedEventHandler click, string? tip = null)
    {
        var b = new Button { Content = text, Padding = new Thickness(8, 1, 8, 1), MinHeight = 26, Margin = new Thickness(6, 0, 0, 0), ToolTip = tip };
        b.Click += click;
        return b;
    }

    /// <summary>A Remove button. With <paramref name="confirm"/>, it asks first — there is no undo.</summary>
    private Button Remove(string what, Action remove, string? confirm = null)
    {
        var b = new Button { Content = "Remove", Padding = new Thickness(8, 1, 8, 1), MinHeight = 26, Margin = new Thickness(6, 0, 0, 0), ToolTip = $"Remove {what}" };
        AutomationProperties.SetName(b, $"Remove {what}");
        b.Click += (_, _) =>
        {
            if (confirm is not null && MessageBox.Show(Window.GetWindow(this), $"Remove {confirm}?\n\nThis cannot be undone.", "Remove",
                                                       MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
            remove();
        };
        return b;
    }

    /// <summary>A line of controls: a fixed-width label and the rest.</summary>
    private static DockPanel Row(string label, UIElement content, double labelWidth = 120)
    {
        var d = new DockPanel { Margin = new Thickness(0, 3, 0, 3), LastChildFill = true };
        var l = new TextBlock { Text = label, Width = labelWidth, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.FindResource("Muted") };
        DockPanel.SetDock(l, Dock.Left);
        d.Children.Add(l);
        d.Children.Add(content);
        return d;
    }

    private static StackPanel Across(params UIElement[] items)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var i in items) p.Children.Add(i);
        return p;
    }

    /// <summary>Buttons for every link Ron keeps (Google Photos, the race website) and for writing to a person.</summary>
    private void LinkBar(TreeNode n)
    {
        var bar = _linkHost!;
        bar.Children.Clear();
        // A friend's profiles first: for many friends this one button is the whole point (Ron, 2026-10-11).
        foreach (var a in n.Children.Where(c => c.Account?.ProfileUrl is not null).Select(c => c.Account!))
        {
            var b = new Button { Content = $"Open {a.Info.Name} profile", Style = (Style)Application.Current.FindResource("Primary"), Margin = new Thickness(0, 0, 8, 8), ToolTip = a.ProfileUrl };
            b.Click += (_, _) => OpenProfile?.Invoke(a, n.Name);
            bar.Children.Add(b);
        }
        foreach (var d in n.Details.Where(d => d.Kind == DetailKind.Link && Uri.TryCreate(d.Value.Trim(), UriKind.Absolute, out _)))
        {
            var b = new Button { Content = $"Open {d.Name}", Style = (Style)Application.Current.FindResource("Primary"), Margin = new Thickness(0, 0, 8, 8), ToolTip = d.Value };
            var url = d.Value.Trim();
            b.Click += (_, _) => MainWindow.OpenInBrowser(url);
            bar.Children.Add(b);
        }
        if (n.Kind == NodeKind.Person)
        {
            var link = new Button { Content = n.Children.Any(c => c.Account is not null) ? "Add another profile…" : "Add a social media profile…", Margin = new Thickness(0, 0, 8, 8),
                                    ToolTip = "Paste the address of their Facebook (or other) profile; then one click opens it." };
            link.Click += (_, _) => AddProfile?.Invoke(n);
            bar.Children.Add(link);
        }
        if (n.Kind == NodeKind.Person && n.Details.FirstOrDefault(d => d.Kind == DetailKind.Email && d.Value.Contains('@', StringComparison.Ordinal)) is { } mail)
        {
            var b = new Button { Content = $"Email {n.Name}", Margin = new Thickness(0, 0, 8, 8), ToolTip = mail.Value };
            b.Click += (_, _) => OpenMail("mailto:" + Uri.EscapeDataString(mail.Value.Trim()));
            bar.Children.Add(b);
        }
    }

    private void NameAndKind(TreeNode n)
    {
        var name = Ui.TextBox("Name", n.Name);
        name.LostFocus += (_, _) =>
        {
            if (name.Text.Trim().Length == 0 || name.Text.Trim() == n.Name) { name.Text = n.Name; return; }
            n.Name = name.Text.Trim();
            Touch();
        };
        _body.Children.Add(Row("Name", name));

        var kinds = new ComboBox { MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(kinds, "Kind");
        if (CurrentTemplate is null) kinds.Items.Add("(none)");   // a kind, once chosen, is changed, not cleared
        foreach (var t in _s.Templates.For(For).OrderBy(t => t.Group).ThenBy(t => t.Name, StringComparer.CurrentCulture))
            kinds.Items.Add(t.Name);
        kinds.SelectedItem = CurrentTemplate?.Name ?? "(none)";
        kinds.SelectionChanged += (_, _) =>
        {
            if (kinds.SelectedItem is not string pick || pick == "(none)" || pick == n.TypeName) return;
            _s.Templates.Find(pick, For)!.ApplyTo(n);
            Touch();
            Render();
        };
        var what = For switch { TemplateFor.Event => "event", TemplateFor.Thing => "thing", _ => "person" };
        _body.Children.Add(Row("Kind", kinds));
        _body.Children.Add(Row("", Hint($"Choosing a kind adds the details usually kept for that kind of {what}. Nothing you typed is lost.")));
    }

    // ---- events ----------------------------------------------------------------------------------

    private void EventPanel(TreeNode n, EventInfo e)
    {
        _body.Children.Add(Heading("When and where"));

        var startDate = new DatePicker { SelectedDate = e.Start?.Date, Width = 150 };
        AutomationProperties.SetName(startDate, "Date");
        var startTime = Ui.TextBox("Start time", e.Start is { } s && !e.AllDay ? s.ToString("t", CultureInfo.CurrentCulture) : "");
        startTime.Width = 100; startTime.Margin = new Thickness(8, 0, 0, 0);
        var allDay = new CheckBox { Content = "All day", IsChecked = e.AllDay, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var endDate = new DatePicker { SelectedDate = e.End?.Date, Width = 150 };
        AutomationProperties.SetName(endDate, "End date");
        var endTime = Ui.TextBox("End time", e.End is { } en && !e.AllDay ? en.ToString("t", CultureInfo.CurrentCulture) : "");
        endTime.Width = 100; endTime.Margin = new Thickness(8, 0, 0, 0);
        var timeHint = Hint("");

        void Apply()
        {
            e.AllDay = allDay.IsChecked == true;
            e.Start = Combine(startDate.SelectedDate, startTime.Text, e.AllDay, out var badStart);
            e.End = Combine(endDate.SelectedDate, endTime.Text, e.AllDay, out var badEnd);
            timeHint.Text = badStart || badEnd ? "Type a time like 7:30 PM or 19:30." : "";
            timeHint.Foreground = Brushes.Firebrick;
            startTime.IsEnabled = endTime.IsEnabled = !e.AllDay;
            CalendarReady();
            Touch();
        }
        startDate.SelectedDateChanged += (_, _) => Apply();
        endDate.SelectedDateChanged += (_, _) => Apply();
        startTime.LostFocus += (_, _) => Apply();
        endTime.LostFocus += (_, _) => Apply();
        allDay.Click += (_, _) => Apply();
        startTime.IsEnabled = endTime.IsEnabled = !e.AllDay;

        _body.Children.Add(Row("Starts", Across(startDate, startTime, allDay)));
        _body.Children.Add(Row("Ends", Across(endDate, endTime, new TextBlock { Text = "  (leave empty if it is one sitting)", Foreground = (Brush)Application.Current.FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center })));
        _body.Children.Add(Row("", timeHint));

        var where = Ui.TextBox("Where", e.Location ?? "");
        where.LostFocus += (_, _) => { e.Location = where.Text.Trim().Length == 0 ? null : where.Text.Trim(); Touch(); };
        _body.Children.Add(Row("Where", where));

        var repeat = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left, ItemsSource = new[] { "Does not repeat", "Every week", "Every month", "Every year" }, SelectedIndex = (int)e.Repeat };
        AutomationProperties.SetName(repeat, "Repeats");
        repeat.SelectionChanged += (_, _) => { e.Repeat = (Repeat)repeat.SelectedIndex; Touch(); };
        _body.Children.Add(Row("Repeats", repeat));

        var status = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left, ItemsSource = new[] { "Thinking about it", "Going", "Went", "Didn't go" }, SelectedIndex = (int)e.Status };
        AutomationProperties.SetName(status, "Am I going");
        status.SelectionChanged += (_, _) => { e.Status = (EventStatus)status.SelectedIndex; Touch(); };
        _body.Children.Add(Row("Am I going?", status));

        _cal = Small("Add to my calendar…", (_, _) => AddToCalendar(n));
        _cal.Margin = new Thickness(0, 6, 0, 0);
        _cal.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTipService.SetShowOnDisabled(_cal, true);
        CalendarReady();
        _body.Children.Add(Row("", _cal));
    }

    private Button? _cal;

    /// <summary>The calendar button works once the event has a date, and says so until then.</summary>
    private void CalendarReady()
    {
        if (_cal is null || _node?.Event is not { } e) return;
        _cal.IsEnabled = e.Start is not null;
        _cal.ToolTip = e.Start is null ? "Give the event a date first (Starts, above)."
                                       : "Makes a calendar file and opens it: Outlook or your calendar program asks to add the event.";
    }

    /// <summary>A date and a typed time; a time that cannot be read is reported and left out.</summary>
    private static DateTime? Combine(DateTime? date, string time, bool allDay, out bool badTime)
    {
        badTime = false;
        if (date is not { } d) return null;
        if (allDay || string.IsNullOrWhiteSpace(time)) return d.Date;
        if (DateTime.TryParse(time, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var t)) return d.Date + t.TimeOfDay;
        badTime = true;
        return d.Date;
    }

    private void AddToCalendar(TreeNode n)
    {
        try
        {
            var safe = string.Concat(n.Name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{safe}.ics");
            System.IO.File.WriteAllText(path, Ics.For(n, DateTime.UtcNow));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (InvalidOperationException ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Add to my calendar", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(Window.GetWindow(this), "No calendar program on this computer opens .ics files. The file was saved in your Temp folder.",
                            "Add to my calendar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>Who is going, how to reach them, and writing to all of them at once.</summary>
    private void Team(TreeNode n)
    {
        var e = n.Event!;
        var contacts = Contacts.For(_s.Tree, n);
        _body.Children.Add(Heading(contacts.Count == 0 ? "Going with" : $"Going with ({contacts.Count})"));
        if (contacts.Count == 0) _body.Children.Add(Hint("No one yet. Add friends from My Friends below, or anyone by name."));

        for (var i = 0; i < contacts.Count; i++)
        {
            var c = contacts[i];
            var who = e.People[i];
            var name = c.Person is null
                ? (UIElement)new TextBlock { Text = c.Name, Width = 200, VerticalAlignment = VerticalAlignment.Center }
                : new Button { Content = c.Name, Style = (Style)Application.Current.FindResource("LinkButton"), Width = 200, ToolTip = $"Open {c.Name}'s page" };
            if (name is Button nb) nb.Click += (_, _) => OpenNode?.Invoke(c.Person!);
            var reach = new TextBlock
            {
                Text = string.Join("   ", new[] { c.Email, c.Phone }.OfType<string>()),
                VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.FindResource("Muted"), Width = 320, TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var row = Across(name, reach);
            if (c.Email is null || c.Phone is null)
                row.Children.Add(Small(c.Email is null && c.Phone is null ? "Add email or phone…" : c.Email is null ? "Add email…" : "Add phone…",
                                       (_, _) => FillContact(c, who), "Their email and phone, for Email everyone and Copy phone numbers"));
            row.Children.Add(Remove($"{c.Name} from this event", () => { e.People.Remove(who); Touch(); Render(); }));
            _body.Children.Add(row);
        }

        // Add a friend from the tree, or a teammate by name with their email and phone.
        var pick = new ComboBox { Width = 220, DisplayMemberPath = "Name" };
        AutomationProperties.SetName(pick, "Choose a friend");
        foreach (var p in _s.Tree.People().Where(p => e.People.All(x => x.PersonId != p.Id)).OrderBy(p => p.Name, StringComparer.CurrentCulture))
            pick.Items.Add(p);
        var add = Small("Add friend", (_, _) =>
        {
            if (pick.SelectedItem is not TreeNode p) { pick.IsDropDownOpen = true; return; }
            e.People.Add(new EventPerson { PersonId = p.Id, Name = p.Name });
            Touch(); Render();
        });
        var addOther = Small("Add someone else…", (_, _) => AddTeammate(e),
            "Someone who is not in My Friends yet: their name, email and phone — and, if you like, add them to My Friends too.");
        addOther.Margin = new Thickness(18, 0, 0, 0);
        _body.Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, 8, 0, 0),
            Children = { Across(new TextBlock { Text = "Friend:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, pick, add), addOther },
        });

        if (contacts.Count == 0) return;
        var mailto = Contacts.MailTo(contacts, n.Name);
        var emailAll = Small("Email everyone", (_, _) => { if (mailto is not null) OpenMail(mailto); }, "Opens your mail program with everyone's address filled in.");
        emailAll.IsEnabled = mailto is not null;
        emailAll.Margin = new Thickness(0);
        var copyMail = Small("Copy emails", (_, _) => Copy(Contacts.EmailList(contacts), "email addresses"), "For pasting into an email's To line.");
        var copyPhone = Small("Copy phone numbers", (_, _) => Copy(Contacts.PhoneList(contacts), "phone numbers"),
            "For a group text: paste them into a new message on your phone, or in Phone Link.");
        _body.Children.Add(new WrapPanel { Margin = new Thickness(0, 10, 0, 0), Children = { emailAll, copyMail, copyPhone } });
        var noMail = Contacts.Missing(contacts, DetailKind.Email);
        var noPhone = Contacts.Missing(contacts, DetailKind.Phone);
        if (noMail.Count + noPhone.Count > 0)
            _body.Children.Add(Hint((noMail.Count > 0 ? $"No email for {string.Join(", ", noMail)}. " : "") +
                                    (noPhone.Count > 0 ? $"No phone for {string.Join(", ", noPhone)}. " : "") +
                                    "Add them on each person's page (an Email or Phone detail).", top: 6));
    }

    /// <summary>A teammate not in My Friends: name, email, phone — and into My Friends too unless Ron unticks it.</summary>
    private void AddTeammate(EventInfo e)
    {
        var dlg = new ContactWindow("Add someone to this event", null, null, null, offerFriends: true) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        if (dlg.AddToFriends)
        {
            var person = TreeNode.Person(dlg.PersonName);
            _s.Templates.Find("Friend", TemplateFor.Person)?.ApplyTo(person);
            if (dlg.Email.Length > 0) person.Set("Email", dlg.Email, DetailKind.Email);
            if (dlg.Phone.Length > 0) person.Set("Phone", dlg.Phone, DetailKind.Phone);
            _s.Tree.Add(_s.Tree.MyFriends, person);
            e.People.Add(new EventPerson { PersonId = person.Id, Name = person.Name });
        }
        else
        {
            e.People.Add(new EventPerson { Name = dlg.PersonName, Email = dlg.Email.Length > 0 ? dlg.Email : null, Phone = dlg.Phone.Length > 0 ? dlg.Phone : null });
        }
        Touch(); Render();
    }

    /// <summary>Fills in a teammate's missing email or phone — on their page if they are a friend, on the event if not.</summary>
    private void FillContact(Contact c, EventPerson who)
    {
        var dlg = new ContactWindow($"How to reach {c.Name}", c.Name, c.Email, c.Phone, offerFriends: false) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() != true) return;
        if (c.Person is { } p)
        {
            if (dlg.Email.Length > 0) Upsert(p, DetailKind.Email, "Email", dlg.Email);
            if (dlg.Phone.Length > 0) Upsert(p, DetailKind.Phone, "Phone", dlg.Phone);
        }
        else
        {
            who.Email = dlg.Email.Length > 0 ? dlg.Email : null;
            who.Phone = dlg.Phone.Length > 0 ? dlg.Phone : null;
        }
        Touch(); Render();

        // Their first detail of that kind ("Email" or "Work email"), or a new one.
        static void Upsert(TreeNode p, DetailKind kind, string name, string value)
        {
            var d = p.Details.FirstOrDefault(x => x.Kind == kind) ?? p.Set(name, "", kind);
            d.Value = value;
        }
    }

    private void Copy(string text, string what)
    {
        if (text.Length == 0) { MessageBox.Show(Window.GetWindow(this), $"There are no {what} to copy yet.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try { Clipboard.SetText(text); } catch (System.Runtime.InteropServices.COMException) { return; }
        MessageBox.Show(Window.GetWindow(this), $"Copied {what}:\n\n{text}\n\nPaste them where you need them (Ctrl+V).", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenMail(string mailto)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mailto) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(Window.GetWindow(this), "No mail program is set up on this computer. Use Copy emails and paste them into your mail instead.",
                            "Email", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>The plan: timed steps in time order.</summary>
    private void Agenda(EventInfo e)
    {
        _body.Children.Add(Heading("Agenda"));
        if (e.Agenda.Count == 0) _body.Children.Add(Hint("The plan, step by step: check-in, the start, cut-off times, dinner after…"));
        foreach (var a in e.Agenda.OrderBy(a => a.At ?? DateTime.MaxValue).ToList())
        {
            var when = new TextBlock { Text = a.At is { } t ? t.ToString("ddd MMM d · h:mm tt", CultureInfo.CurrentCulture) : "(no time)", Width = 190, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            var what = new TextBlock { Text = a.Text + (string.IsNullOrWhiteSpace(a.Where) ? "" : $"  —  {a.Where}"), Width = 420, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            _body.Children.Add(Across(when, what, Remove("this step", () => { e.Agenda.Remove(a); Touch(); Render(); }, $"the step \"{a.Text}\"")));
        }
        var date = new DatePicker { Width = 130, SelectedDate = e.Agenda.LastOrDefault()?.At?.Date ?? e.Start?.Date };
        AutomationProperties.SetName(date, "Step date");
        var time = Ui.TextBox("Step time"); time.Width = 80; time.Margin = new Thickness(6, 0, 0, 0);
        var text = Ui.TextBox("What happens"); text.Width = 240; text.Margin = new Thickness(6, 0, 0, 0);
        var where = Ui.TextBox("Where (optional)"); where.Width = 150; where.Margin = new Thickness(6, 0, 0, 0);
        var stepHint = Hint("Date, time (like 6:00 AM), what happens, and where.");
        var add = Small("Add step", (_, _) =>
        {
            if (text.Text.Trim().Length == 0) { text.Focus(); return; }
            var at = Combine(date.SelectedDate, time.Text, allDay: false, out var badTime);
            if (badTime)
            {
                stepHint.Text = "That time could not be read. Type a time like 6:00 AM or 18:30, or leave it empty.";
                stepHint.Foreground = Brushes.Firebrick;
                time.Focus();
                return;
            }
            e.Agenda.Add(new AgendaItem
            {
                At = at,
                Text = text.Text.Trim(),
                Where = where.Text.Trim().Length == 0 ? null : where.Text.Trim(),
            });
            Touch(); Render();
        });
        _body.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { date, time, text, where, add } });
        _body.Children.Add(stepHint);
    }

    // ---- the name/value details -------------------------------------------------------------------

    private void DetailRows(TreeNode n)
    {
        _body.Children.Add(Heading(n.Kind == NodeKind.Person ? $"About {n.Name}" : "Details"));
        var template = CurrentTemplate;
        if (n.Details.Count == 0) _body.Children.Add(Hint("Nothing kept yet. Choose a kind above, or add any detail below."));

        // A kind brings many suggested details; the empty ones wait behind a button so the filled ones are easy to see.
        var empty = n.Details.Where(d => d.Value.Trim().Length == 0).ToList();
        var fold = !_showEmpty && empty.Count > 4;
        foreach (var d in n.Details.ToList())
        {
            if (fold && empty.Contains(d)) continue;
            var nameBox = new TextBox { Text = d.Name, Width = 190, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0, 0, 0, 1), VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(2, 3, 2, 3) };
            AutomationProperties.SetName(nameBox, "Detail name");
            nameBox.ToolTip = "The detail's name — change it to anything.";
            nameBox.LostFocus += (_, _) => { if (nameBox.Text.Trim().Length > 0 && nameBox.Text.Trim() != d.Name) { d.Name = nameBox.Text.Trim(); Touch(); } else nameBox.Text = d.Name; };

            var editor = Editor(d);
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            DockPanel.SetDock(nameBox, Dock.Left);
            row.Children.Add(nameBox);
            var remove = Remove(d.Name, () => { n.Details.Remove(d); Touch(); Render(); }, d.Value.Trim().Length > 0 ? $"{d.Name} ({d.Value.Trim()})" : null);
            DockPanel.SetDock(remove, Dock.Right);
            remove.VerticalAlignment = VerticalAlignment.Top;
            row.Children.Add(remove);
            if (Action(d) is { } act) { DockPanel.SetDock(act, Dock.Right); act.VerticalAlignment = VerticalAlignment.Top; row.Children.Add(act); }
            ((FrameworkElement)editor).Margin = new Thickness(10, 0, 0, 0);
            row.Children.Add(editor);
            _body.Children.Add(row);
            if (template?.Fields.FirstOrDefault(f => f.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase))?.Hint is { } h)
                _body.Children.Add(new TextBlock { Text = h, Margin = new Thickness(200, 0, 40, 2), TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = (Brush)Application.Current.FindResource("Muted") });
        }

        if (fold)
        {
            var names = string.Join(", ", empty.Take(4).Select(d => d.Name)) + (empty.Count > 4 ? ", …" : "");
            var show = Small($"Show {empty.Count} empty details ({names})", (_, _) => { _showEmpty = true; Render(); },
                             "Details the kind suggests that are not filled in yet.");
            show.Margin = new Thickness(0, 6, 0, 0);
            show.HorizontalAlignment = HorizontalAlignment.Left;
            _body.Children.Add(show);
        }
        AddDetail(n, template);
    }

    /// <summary>The box for a value: a date picker for dates, several lines for a note, one line otherwise.</summary>
    private UIElement Editor(Detail d)
    {
        switch (d.Kind)
        {
            case DetailKind.Date:
            {
                var picker = new DatePicker { SelectedDate = d.AsDate()?.ToDateTime(TimeOnly.MinValue), Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
                AutomationProperties.SetName(picker, d.Name);
                picker.SelectedDateChanged += (_, _) =>
                {
                    d.Value = picker.SelectedDate is { } v ? Dates.Format(DateOnly.FromDateTime(v)) : "";
                    Touch();
                };
                var remind = new CheckBox { Content = "Remind me", IsChecked = d.Remind, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                                            ToolTip = "Show it under Coming up on What's new when it is near." };
                remind.Click += (_, _) => { d.Remind = remind.IsChecked == true; Touch(); };
                return Across(picker, remind);
            }
            case DetailKind.Birthday:
            {
                var box = Ui.TextBox(d.Name, Friendly(d));
                box.Width = 220; box.HorizontalAlignment = HorizontalAlignment.Left;
                box.ToolTip = "Like 3/12/1984, or 3/12 if you don't know the year.";
                var bad = new TextBlock { Foreground = Brushes.Firebrick, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                box.LostFocus += (_, _) =>
                {
                    bad.Text = "";
                    if (box.Text.Trim().Length == 0) { d.Value = ""; Touch(); return; }
                    if (ParseBirthday(box.Text) is not { } v) { bad.Text = "Type it like 3/12/1984, or 3/12 without the year."; return; }
                    d.Value = v;
                    box.Text = Friendly(d);
                    Touch();
                };
                return Across(box, bad);
            }
            case DetailKind.Note:
            {
                var box = Ui.TextBox(d.Name, d.Value);
                box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap; box.MinHeight = 52;
                box.TextChanged += (_, _) => { d.Value = box.Text; Touch(); };
                return box;
            }
            default:
            {
                var box = Ui.TextBox(d.Name, d.Value);
                box.TextChanged += (_, _) => { d.Value = box.Text; Touch(); };
                // A link or an email gets its button at the top of the page as soon as Ron moves on.
                if (d.Kind is DetailKind.Link or DetailKind.Email) box.LostFocus += (_, _) => LinkBar(_node!);
                return box;
            }
        }
    }

    /// <summary>"Open" for a link, "Email" for an address.</summary>
    private static Button? Action(Detail d) => d.Kind switch
    {
        DetailKind.Link => Small("Open", (_, _) => MainWindow.OpenInBrowser(d.Value.Trim()), "Opens it in your web browser"),
        DetailKind.Email => Small("Email", (_, _) =>
        {
            if (d.Value.Contains('@', StringComparison.Ordinal))
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:" + d.Value.Trim()) { UseShellExecute = true }); }
                catch (System.ComponentModel.Win32Exception) { }
        }, "Opens your mail program"),
        _ => null,
    };

    private static string Friendly(Detail d) => d.AsDate() is { } v
        ? Dates.HasYear(v) ? v.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture) : v.ToString("MMMM d", CultureInfo.CurrentCulture)
        : d.Value;

    /// <summary>"3/12/1984", "March 12, 1984", "3/12", "March 12" → "1984-03-12" or "--03-12".</summary>
    internal static string? ParseBirthday(string s)
    {
        s = s.Trim();
        string[] noYear = ["M/d", "MM/dd", "MMMM d", "MMM d", "d MMMM", "d MMM", "M-d"];
        if (DateTime.TryParseExact(s, noYear, CultureInfo.CurrentCulture, DateTimeStyles.None, out var md))
            return Dates.Format(new DateOnly(4, md.Month, md.Day), knownYear: false);
        return Dates.Parse(s) is { } full ? Dates.Format(full, Dates.HasYear(full)) : null;
    }

    private void AddDetail(TreeNode n, Template? template)
    {
        var have = n.Details.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var suggestions = (template?.Fields ?? []).Where(f => !have.Contains(f.Name)).Select(f => (f.Name, f.Kind))
            .Concat(new (string, DetailKind)[] { ("Notes", DetailKind.Note), ("Website", DetailKind.Link), ("Phone", DetailKind.Phone), ("Email", DetailKind.Email) })
            .Concat(n.Kind == NodeKind.Person ? [("Google Photos", DetailKind.Link), ("Birthday", DetailKind.Birthday)] : [])
            .Where(x => !have.Contains(x.Item1)).DistinctBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).ToList();

        var name = new ComboBox { IsEditable = true, Width = 220, Text = "" };
        AutomationProperties.SetName(name, "New detail's name");
        foreach (var s in suggestions) name.Items.Add(s.Item1);
        var kind = new ComboBox { Width = 130, ItemsSource = KindNames, SelectedIndex = 0, Margin = new Thickness(6, 0, 0, 0) };
        AutomationProperties.SetName(kind, "What kind of value");
        name.SelectionChanged += (_, _) =>
        {
            if (name.SelectedItem is string pick && suggestions.FirstOrDefault(x => x.Item1 == pick) is { Item1: not null } x) kind.SelectedIndex = (int)x.Item2;
        };
        var add = Small("Add detail", (_, _) =>
        {
            var text = (name.Text ?? "").Trim();
            if (text.Length == 0) { name.Focus(); return; }
            if (have.Contains(text)) { MessageBox.Show(Window.GetWindow(this), $"There is already a detail called {text}.", "Add detail"); return; }
            var k = (DetailKind)kind.SelectedIndex;
            n.Details.Add(new Detail { Name = text, Kind = k, Remind = k == DetailKind.Birthday });
            Touch(); Render();
        });
        _body.Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, 10, 0, 0),
            Children = { new TextBlock { Text = "Add a detail:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, name, kind, add },
        });
        _body.Children.Add(Hint("Pick a suggestion or type any name — \"Shoe size\", \"Wi-Fi name\", \"Favorite restaurant\". Anything can be kept."));
    }

    private static readonly string[] KindNames = ["Text", "Note (several lines)", "Date", "Birthday", "Number", "Money", "Link", "Phone", "Email"];

    // ---- the log --------------------------------------------------------------------------------

    private void Log(TreeNode n)
    {
        var title = CurrentTemplate?.LogName ?? (n.Kind switch { NodeKind.Person => "Notes", NodeKind.Event => "Notes", _ => "History" });
        _body.Children.Add(Heading(title));
        var hint = n.Kind switch
        {
            NodeKind.Thing => "Dated entries: a service, a repair, a part replaced — with what it cost.",
            NodeKind.Person => "Dated notes: what you talked about, news to remember, what you gave them.",
            _ => "Dated notes about this event.",
        };
        _body.Children.Add(Hint(hint + (n.Kind == NodeKind.Thing ? " Pick the date, say what was done, the cost if any, then Add." : " Pick the date, write the note, then Add.")));

        var date = new DatePicker { SelectedDate = DateTime.Today, Width = 130 };
        AutomationProperties.SetName(date, "Entry date");
        var text = Ui.TextBox("What happened"); text.Width = 360; text.Margin = new Thickness(6, 0, 0, 0);
        var cost = Ui.TextBox("Cost (optional)"); cost.Width = 90; cost.Margin = new Thickness(6, 0, 0, 0);
        cost.ToolTip = "What it cost, if anything";
        var add = Small("Add", (_, _) =>
        {
            if (text.Text.Trim().Length == 0) { text.Focus(); return; }
            n.Log.Add(new LogEntry
            {
                Date = DateOnly.FromDateTime(date.SelectedDate ?? DateTime.Today),
                Text = text.Text.Trim(),
                Cost = cost.Text.Trim().TrimStart('$') is { Length: > 0 } c ? c : null,
            });
            Touch(); Render();
        });
        var addRow = new WrapPanel { Children = { date, text } };
        if (n.Kind == NodeKind.Thing) addRow.Children.Add(cost);
        addRow.Children.Add(add);
        _body.Children.Add(addRow);

        foreach (var entry in n.Log.OrderByDescending(l => l.Date).ToList())
        {
            var line = new TextBlock
            {
                Text = $"{entry.Date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}  ·  {entry.Text}" + (entry.Cost is { } c ? $"  ·  ${c}" : ""),
                TextWrapping = TextWrapping.Wrap, Width = 620, VerticalAlignment = VerticalAlignment.Center,
            };
            _body.Children.Add(Across(line, Remove("this entry", () => { n.Log.Remove(entry); Touch(); Render(); }, $"the entry \"{entry.Text}\"")));
        }
        var total = n.Log.Select(l => decimal.TryParse(l.Cost, NumberStyles.Currency, CultureInfo.CurrentCulture, out var v) ? v : 0m).Sum();
        if (total > 0) _body.Children.Add(Hint($"Total: {total.ToString("C", CultureInfo.CurrentCulture)}", top: 4));
    }

    private void EventsWith(TreeNode person)
    {
        var events = _s.Tree.EventsWith(person).ToList();
        if (events.Count == 0) return;
        _body.Children.Add(Heading($"Events with {person.Name}"));
        foreach (var ev in events)
        {
            var b = new Button
            {
                Content = (ev.Event!.Start is { } s ? s.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture) + "  ·  " : "") + ev.Name,
                Style = (Style)Application.Current.FindResource("LinkButton"), Margin = new Thickness(0, 2, 0, 2),
            };
            b.Click += (_, _) => OpenNode?.Invoke(ev);
            _body.Children.Add(b);
        }
    }

    private void Footer(TreeNode n)
    {
        var save = Small("Save these details as a new kind…", (_, _) => SaveAsKind(n),
            "Next time, choosing this kind fills in the same detail names (not the values).");
        save.Margin = new Thickness(0, 24, 0, 0);
        save.HorizontalAlignment = HorizontalAlignment.Left;
        _body.Children.Add(save);
        _body.Children.Add(Hint("Saved as you type. Not encrypted — never keep passwords here.", top: 8));
    }

    private void SaveAsKind(TreeNode n)
    {
        var name = TextPrompt.Ask(Window.GetWindow(this)!, "Save as a new kind",
            "Name the new kind (like Kayak, or Book club). It will offer these same detail names, empty, for the next one.", n.TypeName ?? "");
        if (name is null) return;
        if (_s.Templates.Find(name, For) is { } old)
        {
            var ok = MessageBox.Show(Window.GetWindow(this), $"There is already a kind called {name}. Replace its list of details with these?",
                                     "Save as a new kind", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (ok != MessageBoxResult.OK) return;
            _s.Templates.Templates.Remove(old);
        }
        var t = Core.Model.Template.From(n, name, For, CurrentTemplate?.LogName);
        t.Group = "Mine";
        _s.Templates.Templates.Add(t);
        _s.SaveTemplates();
        n.TypeName = name;
        Touch();
        Render();
    }
}
