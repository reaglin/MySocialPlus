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
| Repository | **Public**, `github.com/reaglin/MySocialPlus` | Ron, 2026-10-11. So nothing personal goes in it: sample data and tests use made-up people and addresses |
| Social media | **Optional — the program works without it** | Ron, 2026-10-11: *"all social media is optional. It is useful to be able to click and go to a friend's social media."* |
| Two more headings | **My Events** and **My Things** | Ron, 2026-10-11. Events: a concert, dance, race, yoga class… with the people going. Things: a car (license, make/year/model, service…), a house, a coffee maker, a computer, furniture — big and small |
| Information about people and things | **Flexible name/value details**, new kinds by data not code | Ron: *"a flexible structure where an item can be added without changing the code by having flexible name/value pairs."* Built as details (name, value, kind) + templates in `templates.json` + "Save as a new kind" |
| A friend's Facebook | **One click to the profile** | Ron: for many friends *"all I really need is to link to their Facebook profile"* — he pastes the address; the friend's page opens it with one button |
| Google Photos | **A link on the person** | Ron's photos of a friend are a People & pets search in Google Photos; its address is kept as a link and opened with one button. Google no longer lets programs read a library by person, so the program does not show the photos itself |
| Team events | **Agenda + team + contact them all** | Ron's adventure race with three friends: the plan step by step, the team from My Friends, Email everyone, and the email and phone lists to paste into a group text (Windows has no reliable way to start a group text itself) |
| What to build next | **Learn from use** | Ron: *"We are going to learn a lot about what is really useful for this type of app as we use it."* |

## My Friends, My Events, My Things (2026-10-11)

```
TreeNode (Person | Event | Thing)
  TypeName   "Friend", "Race", "Car" — the template it started from
  Details    [ {Name, Value, Kind, Remind} ]   any number, any names, in Ron's order
  Log        [ {Date, Text, Cost} ]            service history, repairs, notes from a talk
  Event      {Start, End, AllDay, Location, Repeat, Status, People[{PersonId|Name}], Agenda[{At, Text, Where}]}
templates.json   [ {Name, For: person|event|thing, Group, Fields[{Name, Kind, Remind, Hint}], LogName} ]
```

- **Kinds** come from `Documents\MySocialPlus\templates.json`, written from the shipped set on the first run;
  after that the file is Ron's. A newer program adds kinds it ships (by the file's `version`) but never
  removes or changes his. Choosing a kind adds its details, empty; nothing typed is ever lost.
- **Values are text**, so nothing is refused; a date is `yyyy-MM-dd`, a birthday without a year `--MM-dd`.
- **Coming up** (What's new) is built from the tree alone: events (following their repeat), birthdays,
  and dates marked Remind me, for the next 30 days, plus reminders up to a week overdue.
- **Not a password store:** tree.json is plain JSON (it is Ron's to back up and read). The pages say so.

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
