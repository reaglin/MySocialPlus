# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**MySocial+** (repo `MySocialPlus`) — a Windows 10/11 program that shows Ron's social media **his
way instead of the algorithm's**: a **topic tree on the left**, the **content on the right**, and a
small **badge on every node that has something new**. Two roots to start: **My Posts** (activity on
what Ron posted — new comments, replies, reactions) and **My Friends** (people, grouped into topics
Ron makes, each with their accounts across networks). Everything in time order; nothing ranked.
WPF on .NET 10; AI through the shared `Eaglin.AiManager`.

**For Ron's personal use — not a Store program** (2026-10-11). His networks: **Bluesky and
Facebook**, mostly.

**Status: plan stage, 2026-10-11** — no code yet. `docs/PLAN.md` has the decisions and what each
network allows; `docs/DEVELOPMENT-PLAN.md` has the phases and the open questions for Ron.

### Read these first

| File | What it is |
|---|---|
| `docs/DEVELOPMENT-PLAN.md` | **The work breakdown** — phases, tasks, marks, open questions. Start here |
| `docs/PLAN.md` | The strategy: what each network's API allows, the decisions with Ron and why, the architecture |

## The facts that shape everything (checked 2026-10-11 — re-check before building each source)

- **Open networks give everything:** Bluesky (AT Protocol), Mastodon, YouTube (API or per-channel
  RSS), blogs/Substack (RSS), Reddit (free tier, personal use). Friends' posts *and* comments on
  Ron's posts.
- **Facebook:** no access to friends' posts. Ron's **own** posts and the comments on them are
  readable; on a **personal profile** Meta usually **hides who commented** — so the app shows "3 new
  comments", not names. A **Page** gives full comment detail. Ron, 2026-10-11: *knowing there is
  activity is worthwhile even without names.*
- **Instagram:** nothing for a personal account. A **Creator/Business** account can read comments on
  its own posts (with usernames).
- **Meta apps used only by their own admins/testers run in Development mode without App Review.**
  That is how this app runs — it is for Ron alone, so it never goes through Meta's App Review.
- **X:** reading needs a paid API plan. Threads/LinkedIn: own posts only. Not planned for now.

## Rules that carry the design

1. **Official APIs and RSS only — never scraping.** Logging in as the user and reading pages breaks
   Facebook's and Instagram's terms and risks Ron's accounts. A friend on a network the app cannot
   read is a node that **opens their profile** (in the app's WebView2), with no badge — and the node
   says why there is no badge.
2. **Time order, never ranked.** Newest first or oldest-unread first; the user chooses. No scoring.
3. **"New" means new since Ron last looked at that node.** Opening a node marks it seen; badges roll
   up the tree (a topic shows the sum of its friends).
4. **The app never posts, likes or replies on its own.** The AI may *draft*; Ron presses the button.
5. **Say where a thing came from and what is missing.** Every item names its network; a comment
   count with no names says Meta withholds them, rather than looking broken.
6. **Data stays on this computer.** Tokens are DPAPI (current user) in `%LOCALAPPDATA%\MySocialPlus`;
   the tree and the cache of posts in `Documents\MySocialPlus` (portfolio rule: data where people look,
   secrets where they do not).
7. **The program is MySocial+ in front of a person, `MySocialPlus` in the code.**

## Planned layout

```
MySocialPlus.slnx
├── src/MySocialPlus.Core/   net10.0, no UI, no Windows API
│   Model/     TreeNode (topic | person | account | post), Person, Account, Item (post or comment),
│              Activity, SeenState
│   Sources/   ISource (fetch since a cursor → items), one folder per network:
│              Bluesky/, Mastodon/, Rss/ (also YouTube channels), Reddit/, Meta/ (Facebook + Instagram)
│   Sync/      Poller (per-source interval, back-off on rate limits), Badges (unread roll-up)
│   Store/     the tree file (JSON) + the item cache (SQLite)
│   Ai/        prompts and a defensive reader for what the AI returns
├── src/MySocialPlus.App/    net10.0-windows, WPF: MainWindow (tree | content), account windows,
│                            AI Settings via Eaglin.AiManager.Wpf, About
└── tests/MySocialPlus.Tests/  xUnit; saved API answers in tests/fixtures (no network in tests)
```

## Conventions

- Portfolio rules apply (`..\CLAUDE.md`): `docs/DEVELOPMENT-PLAN.md` with the three marks (Claude
  never marks ✅), the development cycle with a UX review, the About card "Created by Dr. Ron Eaglin"
  and its softwareplus.ai link, the "+" in the icon.
- `Eaglin.AiManager` (+ `.Wpf`) is a **package from `C:\nuget-local`**, never a project reference —
  pin the latest (**1.6.0** as of 2026-10-11). On a computer that has just cloned AI Manager, run
  `..\AiManager\build\pack.ps1` first: the packages are built locally, not stored in git.
- `Directory.Build.props`: one version property (`MySocialPlusVersion`), nullable, implicit usings,
  **TreatWarningsAsErrors**. Packages MIT/BSD/Apache only.
- `Core` never references a UI assembly. Tests never touch the network — save real API answers as
  fixtures.
- Check the running window with PrintWindow + UI Automation (see `..\LMS-2-Website\CLAUDE.md`).

## Sibling repos worth knowing

- `..\AiManager` — the AI layer.
- `..\GamifyPlus`, `..\TrafficLabPlus` — WPF/.NET 10 Store programs: tree + content windows,
  WebView2, About card, Store packaging to port when the time comes.
- `..\LMS-2-Website` — the model `DEVELOPMENT-PLAN.md`; DPAPI token handling (`TokenStore`).
