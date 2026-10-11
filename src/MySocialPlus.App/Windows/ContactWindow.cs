using System.Windows;
using System.Windows.Controls;

namespace MySocialPlus.App.Windows;

/// <summary>
/// A teammate's name, email and phone in one go — for someone added to an event, or to fill in what a
/// friend's page is missing — and whether they should also be in My Friends.
/// </summary>
public sealed class ContactWindow : Window
{
    private readonly TextBox _name;
    private readonly TextBox _email;
    private readonly TextBox _phone;
    private readonly CheckBox _friend;
    private readonly TextBlock _error;

    public string PersonName => _name.Text.Trim();
    public string Email => _email.Text.Trim();
    public string Phone => _phone.Text.Trim();
    public bool AddToFriends => _friend.IsChecked == true;

    /// <param name="name">Null to ask for a new teammate; a name to fill in that person's email and phone.</param>
    public ContactWindow(string title, string? name, string? email, string? phone, bool offerFriends)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource("AppWindow");

        _name = Ui.TextBox("Name", name ?? "");
        _name.IsEnabled = name is null;
        _email = Ui.TextBox("Email", email ?? "");
        _phone = Ui.TextBox("Phone", phone ?? "");
        _friend = new CheckBox
        {
            Content = "Also add them to My Friends", IsChecked = true, Margin = new Thickness(0, 14, 0, 0),
            Visibility = offerFriends ? Visibility.Visible : Visibility.Collapsed,
            ToolTip = "Then they have a page of their own: birthday, Facebook profile, notes… and they appear in other events' lists.",
        };
        _error = Ui.Text("", top: 8);
        _error.Foreground = System.Windows.Media.Brushes.Firebrick;

        Content = Ui.Stack(
            Ui.Label("Name", 0), _name,
            Ui.Label("Email"), _email,
            Ui.Label("Phone"), _phone,
            Ui.Text("Both are optional. They are used for Email everyone and Copy phone numbers.", hint: true, top: 4),
            _friend, _error,
            Ui.Buttons(
                Ui.Button("OK", (_, _) =>
                {
                    if (PersonName.Length == 0) { _error.Text = "Type their name."; _name.Focus(); return; }
                    if (Email.Length > 0 && !Email.Contains('@', StringComparison.Ordinal)) { _error.Text = "An email address has an @ in it."; _email.Focus(); return; }
                    DialogResult = true;
                }, primary: true, isDefault: true),
                Ui.Button("Cancel", (_, _) => DialogResult = false, isCancel: true)));
        Loaded += (_, _) => (name is null ? _name : _email).Focus();
    }
}
