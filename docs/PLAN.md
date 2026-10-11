# MySocial+ — the plan

## What it is

Ron's feeds are chosen by each network's algorithm. MySocial+ puts them back in his hands:

```
┌──────────────────────────┬──────────────────────────────────────────────┐
│ ▾ My Posts          (4)  │  Sarah Lee · Bluesky · 2 h ago               │
│   ▸ Bluesky         (1)  │  Finally finished the garden path…           │
│   ▸ Facebook        (3)  │  [photo]                                     │
│ ▾ My Friends        (7)  │  ─────────────────────────────────────────── │
│   ▾ Family          (2)  │  Sarah Lee · YouTube · yesterday             │
│     ● Sarah Lee     (2)  │  New video: Path build, part 2               │
│       Tom Eaglin         │                                              │
│   ▸ Colleagues      (5)  │                                              │
│   ▸ Old friends          │                                              │
└──────────────────────────┴──────────────────────────────────────────────┘
```

- **Left: the tree.** Two roots to start, more added later (Ron, 2026-10-11):
  - **My Posts** — Ron's own posts per network, each showing new comments, replies and reactions.
  - **My Friends** — topics Ron makes (Family, Colleagues, …), people inside them, and each
    person's accounts on any number of networks.
- **Right: the content** of whatever is selected — a person's posts from every network merged in
  time order, or a post with its comments.
- **Badges:** a number on any node with something new since Ron last opened it, summed up the tree.

## What each network allows (checked 2026-10-11)

| Network | Friends' posts | Comments on my posts | How |
|---|---|---|---|
| Bluesky | ✅ | ✅ with names | AT Protocol, free; app password or OAuth |
| Mastodon | ✅ | ✅ with names | Instance REST API, OAuth |
| YouTube | ✅ (channels) | ✅ on my videos | Data API v3 (key/OAuth) or per-channel RSS |
| Blogs / Substack | ✅ | — | RSS / Atom |
| Reddit | ✅ (users, subreddits) | ✅ | OAuth, free tier for personal use |
| Facebook | ❌ | ⚠️ profile: counts and text, **names usually hidden**; Page: ✅ full | Graph API, Meta app in Development mode |
| Instagram | ❌ | ❌ personal; ✅ Creator/Business account | Instagram Graph API |
| X | paid API only | paid API only | not planned |
| Threads, LinkedIn | ❌ | own posts only, limited | not planned |

A friend on a network the app cannot read still gets a node: it **opens their profile** inside the
app (WebView2), with no badge and a line saying why.

**Before building the Meta source**, run the 10-minute check in Meta's Graph API Explorer: list
Ron's own recent posts and the comments on one, and note exactly which fields come back (task 5.1).
Meta changes these rules often.

## Decisions (2026-10-11)

| Decision | Choice | Why |
|---|---|---|
| Layout | Topic tree left, content right | Ron's design; the portfolio's familiar shape (Gamify+, Author+) |
| First roots | My Posts, My Friends | Ron; more roots later |
| Activity without names | Worth showing | Ron: *"simply knowing there are comments is worthwhile"* |
| Getting data | Official APIs and RSS only | Scraping breaks Meta's terms and risks Ron's accounts |
| Order | Time, never ranked | The point of the program |
| AI | Through `Eaglin.AiManager`, proposes only | Portfolio rule; never posts for Ron |
| UI | WPF on .NET 10 | Matches the other programs |
| Audience | **Ron only — not on the Store** | Ron. The Meta app runs in Development mode with Ron as admin, so no App Review |
| Networks first | **Bluesky, then Facebook** | Ron uses them most; the rest later |

## What the AI is for (proposals — Ron to choose, open question 6)

- **Catch-up:** "What's new with the Family?" — a few sentences over everything unread under a node.
- **Sort friends into topics** — suggest a topic for a new person from what they post.
- **Draft a reply** to a comment or a friend's post — Ron edits and sends it himself, on the network.
- **Find a thing:** "the post where Tom mentioned the boat" across the local cache.

## Architecture

```
MySocialPlus.Core (net10.0)
  Model/   TreeNode (Topic | Person | Account | Post), Person, Account (network + handle),
           Item (post | comment, network, author or "hidden by network", time, text, media, link),
           SeenState (per node: last opened)
  Sources/ ISource: FetchAsync(account, since) → items, plus what the source can and cannot see
           (Capabilities), so the UI can say why a badge is missing
  Sync/    Poller: each source on its own interval, back-off on rate limits; Badges: unread roll-up
  Store/   tree.json (Ron's topics and people — small, hand-readable, easy to back up);
           items.db (SQLite cache, rebuildable from the networks)
  Ai/      prompts, AiReader (defensive parse of what the model returns)

MySocialPlus.App (net10.0-windows, WPF)
  MainWindow: TreeView with badges | content pane; account sign-in windows; profile viewer (WebView2);
  AI Settings from Eaglin.AiManager.Wpf; About
```

Tokens: DPAPI current user, `%LOCALAPPDATA%\MySocialPlus`. Data: `Documents\MySocialPlus`.
