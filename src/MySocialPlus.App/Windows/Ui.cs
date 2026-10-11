using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace MySocialPlus.App.Windows;

/// <summary>Small builders so the dialogs (made in code) look alike and are labelled for screen readers.</summary>
internal static class Ui
{
    public static Window Dialog(string title, double width, double height = double.NaN)
    {
        var w = new Window
        {
            Title = title,
            Width = width,
            SizeToContent = double.IsNaN(height) ? SizeToContent.Height : SizeToContent.Manual,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            ResizeMode = double.IsNaN(height) ? ResizeMode.NoResize : ResizeMode.CanResize,
            Style = (Style)Application.Current.FindResource("AppWindow"),
        };
        if (!double.IsNaN(height)) w.Height = height;
        return w;
    }

    public static TextBlock Text(string text, bool hint = false, double top = 0) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, top, 0, 0),
        Style = hint ? (Style)Application.Current.FindResource("Hint") : null,
    };

    public static TextBlock Label(string text, double top = 12) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 4), TextWrapping = TextWrapping.Wrap,
    };

    public static TextBox TextBox(string label, string value = "")
    {
        var t = new TextBox { Text = value, Padding = new Thickness(4, 3, 4, 3) };
        AutomationProperties.SetName(t, label);
        return t;
    }

    public static Button Button(string text, RoutedEventHandler click, bool primary = false, bool isDefault = false, bool isCancel = false)
    {
        var b = new Button { Content = text, Margin = new Thickness(8, 0, 0, 0), IsDefault = isDefault, IsCancel = isCancel, MinWidth = 90 };
        if (primary) b.Style = (Style)Application.Current.FindResource("Primary");
        b.Click += click;
        return b;
    }

    public static Button Link(string text, string url)
    {
        var b = new Button { Content = text, Style = (Style)Application.Current.FindResource("LinkButton"), HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += (_, _) => MainWindow.OpenInBrowser(url);
        return b;
    }

    /// <summary>Buttons in a row at the bottom right.</summary>
    public static StackPanel Buttons(params UIElement[] buttons)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        foreach (var b in buttons) p.Children.Add(b);
        return p;
    }

    public static StackPanel Stack(params UIElement[] children)
    {
        var p = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }
}

/// <summary>Asks for one line of text (a topic's name). Null when cancelled.</summary>
public static class TextPrompt
{
    public static string? Ask(Window owner, string title, string prompt, string initial)
    {
        var w = Ui.Dialog(title, 440);
        w.Owner = owner;
        var box = Ui.TextBox(prompt, initial);
        var error = Ui.Text("", hint: true, top: 6);
        error.Foreground = System.Windows.Media.Brushes.Firebrick;
        string? result = null;
        w.Content = Ui.Stack(
            Ui.Text(prompt),
            Ui.Label("Name", 10), box, error,
            Ui.Buttons(
                Ui.Button("OK", (_, _) =>
                {
                    if (string.IsNullOrWhiteSpace(box.Text)) { error.Text = "Type a name, or press Cancel."; return; }
                    result = box.Text.Trim();
                    w.DialogResult = true;
                }, primary: true, isDefault: true),
                Ui.Button("Cancel", (_, _) => w.DialogResult = false, isCancel: true)));
        w.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return w.ShowDialog() == true ? result : null;
    }
}
