using System.IO;
using System.Windows;
using System.Windows.Threading;
using MySocialPlus.Core.Sample;
using MySocialPlus.Core.Store;

namespace MySocialPlus.App;

/// <summary>
/// Starts the program. Command line (for checking the window without touching Ron's data):
/// <list type="bullet">
/// <item><c>--sample</c> — open a fresh made-up tree and cache in %TEMP%; no network is read.</item>
/// <item><c>--write-sample &lt;folder&gt;</c> — write the sample there and exit.</item>
/// <item><c>--data &lt;folder&gt;</c> — use that folder instead of Documents\MySocialPlus.</item>
/// </list>
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        var args = e.Args;
        string? Arg(string name)
        {
            var i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        if (Arg("--write-sample") is { } target)
        {
            SampleData.Write(DataPaths.InFolder(Path.GetFullPath(target)), DateTimeOffset.Now);
            Shutdown();
            return;
        }

        DataPaths paths;
        var sample = args.Any(a => string.Equals(a, "--sample", StringComparison.OrdinalIgnoreCase));
        if (sample)
        {
            paths = DataPaths.InFolder(Path.Combine(Path.GetTempPath(), "MySocialPlus-sample"));
            SampleData.Write(paths, DateTimeOffset.Now);
        }
        else
        {
            paths = Arg("--data") is { } folder ? DataPaths.InFolder(Path.GetFullPath(folder)) : DataPaths.Default;
        }

        AppSession session;
        try { session = AppSession.Open(paths, readNetworks: !sample); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"MySocial+ could not open its data in\n{paths.DataFolder}\n\n{ex.Message}\n\n" +
                "If tree.json was edited by hand, check it, or move it aside to start with an empty tree.",
                "MySocial+", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var main = new MainWindow(session);
        MainWindow = main;
        main.Show();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Something went wrong that MySocial+ did not expect:\n\n" + e.Exception.Message +
            "\n\nYour tree is saved after every change, so nothing should be lost. If this keeps happening, tell Claude what you were doing.",
            "MySocial+", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
