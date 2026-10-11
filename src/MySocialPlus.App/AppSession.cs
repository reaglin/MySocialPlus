using System.Net.Http;
using MySocialPlus.Core.Model;
using MySocialPlus.Core.Secrets;
using MySocialPlus.Core.Sources;
using MySocialPlus.Core.Sources.Bluesky;
using MySocialPlus.Core.Store;
using MySocialPlus.Core.Sync;

namespace MySocialPlus.App;

/// <summary>Everything the window works on: the tree, the cache, the secrets and the sources.</summary>
public sealed class AppSession : IDisposable
{
    private AppSession(DataPaths paths, SocialTree tree, ItemCache cache, bool readNetworks)
    {
        Paths = paths;
        Tree = tree;
        Cache = cache;
        ReadsNetworks = readNetworks;
        Secrets = new SecretStore(paths.SecretsFolder);
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MySocialPlus/{typeof(AppSession).Assembly.GetName().Version}");
        Bluesky = new BlueskySource(new BlueskyClient(http), Secrets);
        Sync = new SyncService(tree, cache, [Bluesky], new PollSchedule());
    }

    public static AppSession Open(DataPaths paths, bool readNetworks) =>
        new(paths, TreeStore.Load(paths.TreeFile), new ItemCache(paths.ItemsDb), readNetworks)
        {
            Templates = TemplateStore.Load(paths.TemplatesFile),
        };

    /// <summary>The kinds of people, events and things (templates.json — Ron's to edit).</summary>
    public TemplateSet Templates { get; private init; } = new();

    public void SaveTemplates() { if (ReadsNetworks) TemplateStore.Save(Templates, Paths.TemplatesFile); }

    public DataPaths Paths { get; }
    public SocialTree Tree { get; }
    public ItemCache Cache { get; }
    public SecretStore Secrets { get; }
    public BlueskySource Bluesky { get; }
    public SyncService Sync { get; }

    /// <summary>False for the sample: its accounts are made up, so nothing is read.</summary>
    public bool ReadsNetworks { get; }

    public void Save() => TreeStore.Save(Tree, Paths.TreeFile);

    public void Dispose() => Cache.Dispose();
}
