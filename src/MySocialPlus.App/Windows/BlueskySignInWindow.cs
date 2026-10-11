using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MySocialPlus.Core.Model;
using MySocialPlus.Core.Secrets;
using MySocialPlus.Core.Sources;
using MySocialPlus.Core.Sources.Bluesky;

namespace MySocialPlus.App.Windows;

/// <summary>
/// Connects Ron's own Bluesky account with an app password (plan 3.1): paste, check, save, remove.
/// The app password is checked by signing in before it is saved, and saved DPAPI-encrypted.
/// </summary>
public sealed class BlueskySignInWindow : Window
{
    private readonly AppSession _s;
    private readonly TextBox _handle;
    private readonly PasswordBox _password;
    private readonly TextBlock _status;
    private readonly Button _save;
    private readonly Button _remove;
    private readonly Button _close;
    private readonly Button _next;

    /// <summary>True when an account was added or a sign-in saved or removed.</summary>
    public bool Changed { get; private set; }
    public string? AccountNodeId { get; private set; }
    /// <summary>Ron pressed "Choose people I follow…" after signing in: step 2 opens next.</summary>
    public bool WantsImport { get; private set; }

    public BlueskySignInWindow(AppSession session)
    {
        _s = session;
        Title = "Bluesky sign-in";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource("AppWindow");

        var existing = Mine();
        _handle = Ui.TextBox("Your Bluesky handle", existing?.Handle ?? "");
        _password = new PasswordBox { Padding = new Thickness(4, 3, 4, 3) };
        AutomationProperties.SetName(_password, "App password");
        _status = Ui.Text("", top: 10);
        _save = Ui.Button("Check and save", async (_, _) => await SaveAsync(), primary: true, isDefault: true);
        _remove = Ui.Button("Remove sign-in", (_, _) => Remove());
        _close = Ui.Button("Close", (_, _) => Close(), isCancel: true);
        _next = Ui.Button("Choose people I follow…", (_, _) => { WantsImport = true; Close(); }, primary: true);
        _next.Visibility = Visibility.Collapsed;

        Content = Ui.Stack(
            Ui.Text("MySocial+ reads your Bluesky notifications — who replied to, liked or shared your posts — with an app password. " +
                    "An app password is not your real password: it can be removed in Bluesky at any time, and it cannot change your password or delete your account."),
            Ui.Label("1. Make an app password in Bluesky", 14),
            Ui.Text("In Bluesky: Settings ▸ Privacy and security ▸ App passwords ▸ Add App Password. Name it MySocial+. " +
                    "Leave \"Allow access to your direct messages\" unticked — MySocial+ does not need it.", hint: true),
            Ui.Link("Open Bluesky's App passwords page", "https://bsky.app/settings/app-passwords"),
            Ui.Label("2. Your handle", 14),
            _handle,
            Ui.Text("Like roneaglin.bsky.social — it is under your name on your profile.", hint: true, top: 4),
            Ui.Label("3. Paste the app password", 14),
            _password,
            Ui.Text("It looks like abcd-efgh-ijkl-mnop. It is kept on this computer only, encrypted so only your Windows account can read it.", hint: true, top: 4),
            _status,
            Ui.Buttons(_next, _save, _remove, _close));

        if (existing is not null && _s.Secrets.Has(BlueskySource.SecretName(existing.Handle)))
            _status.Text = $"Signed in as @{existing.Handle} (app password {SecretStore.Mask(_s.Secrets.Load(BlueskySource.SecretName(existing.Handle)) ?? "")}). To change it, paste a new app password and press Check and save.";
        _remove.IsEnabled = existing is not null && _s.Secrets.Has(BlueskySource.SecretName(existing.Handle));
        Loaded += (_, _) => (_handle.Text.Length == 0 ? (Control)_handle : _password).Focus();
    }

    private Account? Mine() => _s.Tree.MyPosts.Children.Select(c => c.Account).FirstOrDefault(a => a?.Network == Network.Bluesky);

    private async Task SaveAsync()
    {
        var handle = BlueskyClient.Clean(_handle.Text);
        var password = _password.Password.Trim();
        if (handle.Length == 0 || !handle.Contains('.', StringComparison.Ordinal)) { Say("Type your handle, like roneaglin.bsky.social.", bad: true); _handle.Focus(); return; }
        if (password.Length == 0) { Say("Paste the app password you made in Bluesky.", bad: true); _password.Focus(); return; }

        _save.IsEnabled = false;
        Say("Checking with Bluesky…");
        try
        {
            var who = await _s.Bluesky.Client.SignInAsync(handle, password, CancellationToken.None);
            BlueskyActor? profile = null;
            try { profile = await _s.Bluesky.Client.GetProfileAsync(who.Did, CancellationToken.None); }
            catch (SourceException) { /* the name and picture are a nicety */ }

            var account = _s.Tree.MyPosts.Children.Select(c => c.Account)
                              .FirstOrDefault(a => a?.Network == Network.Bluesky && (a.Id == who.Did || BlueskyClient.Clean(a.Handle) == handle));
            if (account is null)
            {
                account = new Account { Network = Network.Bluesky, Handle = who.Handle, Id = who.Did, IsMine = true };
                var node = TreeNode.ForAccount(account);
                _s.Tree.Add(_s.Tree.MyPosts, node);
                AccountNodeId = node.Id;
            }
            else
            {
                AccountNodeId = _s.Tree.AllNodes().First(n => ReferenceEquals(n.Account, account)).Id;
            }
            account.Id ??= who.Did;
            account.Handle = who.Handle;
            account.DisplayName = profile?.DisplayName ?? account.DisplayName;
            account.AvatarUrl = profile?.AvatarUrl ?? account.AvatarUrl;
            account.LastError = null;
            _s.Secrets.Save(BlueskySource.SecretName(who.Handle), password);
            _password.Clear();
            Changed = true;
            _remove.IsEnabled = true;
            Say($"Signed in as @{who.Handle} and saved. MySocial+ reads your posts as soon as this window closes. " +
                "Next, choose the people you follow that you want in MySocial+ — or press Done and do it later from the Accounts menu.");
            // Done now, and Enter goes on to step 2 rather than re-checking an empty password box.
            _save.IsDefault = false;
            _close.Content = "Done";
            _next.Visibility = Visibility.Visible;
            _next.IsDefault = true;
            _next.Focus();
        }
        catch (SourceException e)
        {
            Say(e.Message, bad: true);
        }
        finally { _save.IsEnabled = true; }
    }

    private void Remove()
    {
        var a = Mine();
        if (a is null) return;
        var ok = MessageBox.Show(this,
            $"Remove the saved app password for @{a.Handle}?\n\nMySocial+ will stop reading your notifications until you sign in again. " +
            "Your account stays in My Posts. To cancel the app password itself, remove it in Bluesky's App passwords page too.",
            "Remove sign-in", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (ok != MessageBoxResult.OK) return;
        _s.Secrets.Delete(BlueskySource.SecretName(a.Handle));
        _s.Bluesky.Client.SignOut(a.Handle);
        Changed = true;
        _remove.IsEnabled = false;
        Say("Removed. MySocial+ no longer has an app password for this account.");
    }

    private void Say(string text, bool bad = false)
    {
        _status.Text = text;
        _status.Foreground = bad ? System.Windows.Media.Brushes.Firebrick : (System.Windows.Media.Brush)Application.Current.FindResource("Accent");
    }

}
