using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MySocialPlus.Core.Model;

namespace MySocialPlus.App.Windows;

/// <summary>
/// New event or new thing: a name, its kind (which brings the usual details), and for an event its
/// date. Everything else is filled in on its page afterwards.
/// </summary>
public sealed class NewItemWindow : Window
{
    private readonly TemplateFor _for;
    private readonly TextBox _name;
    private readonly ComboBox _kind = new() { MinWidth = 280 };
    private readonly DatePicker _date = new() { Width = 160 };
    private readonly TextBox _time;
    private readonly TextBlock _error;

    public TreeNode? Result { get; private set; }

    public NewItemWindow(TemplateSet templates, TemplateFor forWhat, string groupName)
    {
        _for = forWhat;
        var isEvent = forWhat == TemplateFor.Event;
        Title = isEvent ? "New event" : "New thing";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource("AppWindow");

        _name = Ui.TextBox(isEvent ? "Event name" : "What it is called");
        AutomationProperties.SetName(_kind, "Kind");
        string? group = null;
        foreach (var t in templates.For(forWhat).OrderBy(t => t.Group).ThenBy(t => t.Name, StringComparer.CurrentCulture))
        {
            if (t.Group != group)
            {
                group = t.Group;
                _kind.Items.Add(new ComboBoxItem { Content = group ?? "Other", IsEnabled = false, FontWeight = FontWeights.SemiBold });
            }
            _kind.Items.Add(new ComboBoxItem { Content = "   " + t.Name, Tag = t });
        }
        _kind.SelectedItem = _kind.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag is Template);
        AutomationProperties.SetName(_date, "Date");
        _time = Ui.TextBox("Time");
        _time.Width = 100;
        _time.Margin = new Thickness(8, 0, 0, 0);
        _error = Ui.Text("", top: 8);
        _error.Foreground = System.Windows.Media.Brushes.Firebrick;

        var stack = Ui.Stack(
            Ui.Text(isEvent
                ? $"Plan something you are going to: a race, a concert, a class, a trip. It goes in {groupName}. Who is going, the agenda and the rest are on its page next."
                : $"Something you own and want to keep track of. It goes in {groupName}. Its details and history are on its page next."),
            Ui.Label("Name"), _name,
            Ui.Text(isEvent ? "Like: Turkey Trot 5K, Jazz on the River" : "Like: Camry, Our house, Coffee maker", hint: true, top: 4),
            Ui.Label("Kind"), _kind,
            Ui.Text("Brings the details usually kept for it. You can add or remove any of them later.", hint: true, top: 4));
        if (isEvent)
        {
            stack.Children.Add(Ui.Label("When (you can leave it for later)"));
            stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { _date, _time } });
            stack.Children.Add(Ui.Text("A time like 7:30 PM; leave the time empty for an all-day event.", hint: true, top: 4));
        }
        stack.Children.Add(_error);
        stack.Children.Add(Ui.Buttons(
            Ui.Button(isEvent ? "Make the event" : "Add the thing", (_, _) => Ok(), primary: true, isDefault: true),
            Ui.Button("Cancel", (_, _) => DialogResult = false, isCancel: true)));
        Content = stack;
        Loaded += (_, _) => _name.Focus();
    }

    private void Ok()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0) { _error.Text = "Type a name."; _name.Focus(); return; }
        var template = (_kind.SelectedItem as ComboBoxItem)?.Tag as Template;
        TreeNode node;
        if (_for == TemplateFor.Event)
        {
            DateTime? start = null;
            var allDay = false;
            if (_date.SelectedDate is { } d)
            {
                if (_time.Text.Trim().Length == 0) { start = d.Date; allDay = true; }
                else if (DateTime.TryParse(_time.Text, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var t)) start = d.Date + t.TimeOfDay;
                else { _error.Text = "Type a time like 7:30 PM or 19:30, or leave it empty."; _time.Focus(); return; }
            }
            node = TreeNode.NewEvent(name, start);
            node.Event!.AllDay = allDay;
            node.Event.Status = EventStatus.Going;
        }
        else node = TreeNode.Thing(name);
        template?.ApplyTo(node);
        Result = node;
        DialogResult = true;
    }
}
