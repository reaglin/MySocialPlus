using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MySocialPlus.App.Windows;

/// <summary>
/// A friend's profile on a network MySocial+ cannot read (Facebook, Instagram, …), shown inside the
/// program (CLAUDE.md, rule 1). Ron reads it himself, signed in as himself; MySocial+ does not read
/// the page. The browser data (his sign-in) is kept in %LOCALAPPDATA%\MySocialPlus\WebView2.
/// </summary>
public sealed class ProfileWindow : Window
{
    public ProfileWindow(string title, string url, string dataFolder)
    {
        Title = title;
        Width = 1000; Height = 800;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Style = (Style)Application.Current.FindResource("AppWindow");

        var note = Ui.Text("This is the network's own page. MySocial+ only shows it — sign in here the first time if the network asks.", hint: true);
        note.Margin = new Thickness(12, 8, 12, 8);
        var browserButton = new Button { Content = "Open in my web browser", Margin = new Thickness(0, 6, 12, 6), Padding = new Thickness(10, 2, 10, 2) };
        browserButton.Click += (_, _) => MainWindow.OpenInBrowser(url);
        var bar = new DockPanel { Background = (System.Windows.Media.Brush)Application.Current.FindResource("Pane") };
        DockPanel.SetDock(browserButton, Dock.Right);
        bar.Children.Add(browserButton);
        bar.Children.Add(note);

        var web = new WebView2 { CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = dataFolder } };
        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);
        dock.Children.Add(web);
        Content = dock;

        Loaded += async (_, _) =>
        {
            try
            {
                await web.EnsureCoreWebView2Async();
                web.CoreWebView2.Navigate(url);
            }
            catch (Exception e) when (e is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                note.Text = "The page could not be shown inside MySocial+ (" + e.Message + "). Use Open in my web browser.";
            }
        };
    }
}
