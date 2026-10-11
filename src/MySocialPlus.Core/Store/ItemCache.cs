using Microsoft.Data.Sqlite;
using MySocialPlus.Core.Model;

namespace MySocialPlus.Core.Store;

/// <summary>
/// The cache of everything read from the networks (items.db). It can always be rebuilt from the
/// networks, so it holds no work of Ron's except what he has seen. Items are keyed by account and
/// network id, so the same post can belong to Ron (a reply to him) and to the friend who wrote it.
/// </summary>
public sealed class ItemCache : IDisposable
{
    private readonly SqliteConnection _db;

    public ItemCache(string path)
    {
        if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _db.Open();
        Exec("""
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS items (
                account_key  TEXT NOT NULL,
                id           TEXT NOT NULL,
                network      TEXT NOT NULL,
                kind         TEXT NOT NULL,
                parent_id    TEXT,
                author_name  TEXT,
                author_handle TEXT,
                author_avatar TEXT,
                created_at   INTEGER NOT NULL,
                fetched_at   INTEGER NOT NULL,
                text         TEXT NOT NULL,
                image_url    TEXT,
                link         TEXT,
                attachment   TEXT,
                context      TEXT,
                count        INTEGER NOT NULL DEFAULT 1,
                likes        INTEGER NOT NULL DEFAULT 0,
                replies      INTEGER NOT NULL DEFAULT 0,
                reposts      INTEGER NOT NULL DEFAULT 0,
                seen         INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (account_key, id)
            );
            CREATE INDEX IF NOT EXISTS items_by_time ON items (account_key, created_at DESC);
            CREATE INDEX IF NOT EXISTS items_unseen ON items (seen, account_key);
            """);
    }

    /// <summary>
    /// Adds new items and refreshes ones already here (text and counts change; whether Ron has seen
    /// it does not). New items arrive unseen unless <paramref name="asSeen"/> — used for an account's
    /// first read, so connecting does not light up a year of posts. Returns how many were new.
    /// </summary>
    public int Upsert(IEnumerable<Item> items, bool asSeen = false)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = _db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO items (account_key, id, network, kind, parent_id, author_name, author_handle, author_avatar,
                               created_at, fetched_at, text, image_url, link, attachment, context,
                               count, likes, replies, reposts, seen)
            VALUES ($ak, $id, $net, $kind, $parent, $an, $ah, $av, $created, $fetched, $text, $img, $link, $att, $ctx,
                    $count, $likes, $replies, $reposts, $seen)
            ON CONFLICT (account_key, id) DO UPDATE SET
                author_name = excluded.author_name, author_handle = excluded.author_handle,
                author_avatar = excluded.author_avatar, text = excluded.text, image_url = excluded.image_url,
                link = excluded.link, attachment = excluded.attachment, context = excluded.context,
                likes = excluded.likes, replies = excluded.replies, reposts = excluded.reposts,
                seen = CASE WHEN excluded.count > items.count THEN 0 ELSE items.seen END,
                count = excluded.count, fetched_at = excluded.fetched_at;
            """;

        // An upsert does not say whether it inserted, so count the rows before and after.
        var before = CountAll(tx);
        var p = new Dictionary<string, SqliteParameter>();
        foreach (var name in new[] { "$ak", "$id", "$net", "$kind", "$parent", "$an", "$ah", "$av", "$created", "$fetched",
                                     "$text", "$img", "$link", "$att", "$ctx", "$count", "$likes", "$replies", "$reposts", "$seen" })
            p[name] = cmd.Parameters.Add(new SqliteParameter { ParameterName = name });

        foreach (var i in items)
        {
            p["$ak"].Value = i.AccountKey;
            p["$id"].Value = i.Id;
            p["$net"].Value = i.Network.ToString();
            p["$kind"].Value = i.Kind.ToString();
            p["$parent"].Value = (object?)i.ParentId ?? DBNull.Value;
            p["$an"].Value = (object?)i.AuthorName ?? DBNull.Value;
            p["$ah"].Value = (object?)i.AuthorHandle ?? DBNull.Value;
            p["$av"].Value = (object?)i.AuthorAvatarUrl ?? DBNull.Value;
            p["$created"].Value = i.CreatedAt.ToUnixTimeMilliseconds();
            p["$fetched"].Value = i.FetchedAt.ToUnixTimeMilliseconds();
            p["$text"].Value = i.Text;
            p["$img"].Value = (object?)i.ImageUrl ?? DBNull.Value;
            p["$link"].Value = (object?)i.Link ?? DBNull.Value;
            p["$att"].Value = (object?)i.Attachment ?? DBNull.Value;
            p["$ctx"].Value = (object?)i.Context ?? DBNull.Value;
            p["$count"].Value = i.Count;
            p["$likes"].Value = i.Likes;
            p["$replies"].Value = i.Replies;
            p["$reposts"].Value = i.Reposts;
            p["$seen"].Value = asSeen || i.Seen ? 1 : 0;
            cmd.ExecuteNonQuery();
        }
        var added = (int)(CountAll(tx) - before);
        tx.Commit();
        return added;
    }

    private long CountAll(SqliteTransaction tx)
    {
        using var c = _db.CreateCommand();
        c.Transaction = tx;
        c.CommandText = "SELECT COUNT(*) FROM items";
        return (long)c.ExecuteScalar()!;
    }

    /// <summary>Everything for these accounts, newest first. Comments and reactions come with their posts.</summary>
    public List<Item> ForAccounts(IReadOnlyCollection<string> accountKeys, int limit = 3000)
    {
        if (accountKeys.Count == 0) return [];
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"SELECT * FROM items WHERE account_key IN ({InList(cmd, accountKeys)}) ORDER BY created_at DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        return Read(cmd);
    }

    /// <summary>Unseen count per account (a reaction item standing for 5 likes counts 5).</summary>
    public Dictionary<string, int> UnseenByAccount()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT account_key, SUM(count) FROM items WHERE seen = 0 GROUP BY account_key";
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        using var r = cmd.ExecuteReader();
        while (r.Read()) d[r.GetString(0)] = (int)r.GetInt64(1);
        return d;
    }

    /// <summary>Marks everything for these accounts as seen — what opening a node does.</summary>
    public int MarkSeen(IReadOnlyCollection<string> accountKeys)
    {
        if (accountKeys.Count == 0) return 0;
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"UPDATE items SET seen = 1 WHERE seen = 0 AND account_key IN ({InList(cmd, accountKeys)})";
        return cmd.ExecuteNonQuery();
    }

    /// <summary>"Mark as new again": undoes opening a node, for exactly the items that were new then.</summary>
    public int MarkUnseen(IEnumerable<(string AccountKey, string Id)> items)
    {
        using var tx = _db.BeginTransaction();
        using var cmd = _db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE items SET seen = 0 WHERE account_key = $k AND id = $id";
        var k = cmd.Parameters.Add(new SqliteParameter { ParameterName = "$k" });
        var id = cmd.Parameters.Add(new SqliteParameter { ParameterName = "$id" });
        var n = 0;
        foreach (var (accountKey, itemId) in items)
        {
            k.Value = accountKey;
            id.Value = itemId;
            n += cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return n;
    }

    /// <summary>Removes an account's items (the account was deleted from the tree).</summary>
    public int Forget(string accountKey)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE account_key = $k";
        cmd.Parameters.AddWithValue("$k", accountKey);
        return cmd.ExecuteNonQuery();
    }

    public int Count()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items";
        return (int)(long)cmd.ExecuteScalar()!;
    }

    private static string InList(SqliteCommand cmd, IEnumerable<string> values)
    {
        var names = new List<string>();
        var n = 0;
        foreach (var v in values)
        {
            var name = "$k" + n++;
            cmd.Parameters.AddWithValue(name, v);
            names.Add(name);
        }
        return string.Join(",", names);
    }

    private static List<Item> Read(SqliteCommand cmd)
    {
        var list = new List<Item>();
        using var r = cmd.ExecuteReader();
        string? S(string col) { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetString(o); }
        int I(string col) => (int)r.GetInt64(r.GetOrdinal(col));
        while (r.Read())
        {
            list.Add(new Item
            {
                AccountKey = S("account_key")!,
                Id = S("id")!,
                Network = Enum.TryParse<Network>(S("network"), out var net) ? net : Network.Other,
                Kind = Enum.TryParse<ItemKind>(S("kind"), out var kind) ? kind : ItemKind.Post,
                ParentId = S("parent_id"),
                AuthorName = S("author_name"),
                AuthorHandle = S("author_handle"),
                AuthorAvatarUrl = S("author_avatar"),
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(r.GetOrdinal("created_at"))),
                FetchedAt = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(r.GetOrdinal("fetched_at"))),
                Text = S("text") ?? "",
                ImageUrl = S("image_url"),
                Link = S("link"),
                Attachment = S("attachment"),
                Context = S("context"),
                Count = I("count"),
                Likes = I("likes"),
                Replies = I("replies"),
                Reposts = I("reposts"),
                Seen = I("seen") != 0,
            });
        }
        return list;
    }

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
