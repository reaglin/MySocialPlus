using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MySocialPlus.App.Windows;

/// <summary>Help ▸ About: what the program is, and the "Created by Dr. Ron Eaglin" card (portfolio rule).</summary>
public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = "About MySocial+";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Style = (Style)Application.Current.FindResource("AppWindow");

        var version = typeof(AboutWindow).Assembly.GetName().Version;
        var picture = new Border
        {
            Width = 96, Height = 96, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Top,
            Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Images/dr-ron-eaglin.jpg"))) { Stretch = Stretch.UniformToFill },
        };
        var words = new StackPanel();
        words.Children.Add(new TextBlock { Text = "Created by Dr. Ron Eaglin", FontSize = 18, FontWeight = FontWeights.SemiBold });
        words.Children.Add(Ui.Text("Social media in your order: the people you choose, in topics you make, newest first — never ranked by an algorithm.", hint: true, top: 4));
        var card = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        DockPanel.SetDock(picture, Dock.Left);
        card.Children.Add(picture);
        card.Children.Add(words);

        Content = Ui.Stack(
            new TextBlock { Text = "MySocial+", FontSize = 24, FontWeight = FontWeights.SemiBold },
            Ui.Text($"Version {version?.ToString(3)}", hint: true),
            card,
            Ui.Text("MySocial+ reads only through each network's official API — it never logs in as you to read pages, and it never posts, " +
                    "likes or replies for you. Everything it reads stays on this computer.", top: 16),
            Ui.Text("More from Dr. Ron Eaglin, with help and examples:", top: 12),
            Ui.Link("softwareplus.ai", "https://softwareplus.ai/"),
            Ui.Buttons(Ui.Button("Close", (_, _) => Close(), isDefault: true, isCancel: true)));
    }
}
