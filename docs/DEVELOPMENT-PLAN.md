# MySocial+ — development plan

Numbered tasks per phase, each with what "done" means. **Started 2026-10-11** (plan only).
Personal use; Ron's networks are **Bluesky and Facebook** (2026-10-11). The phases prove the
tree-and-badges idea on Bluesky, the easy one, before Facebook, the hard one.

---

## How an item is marked

| Mark | Means |
|---|---|
| ⬜ | to do — not started or still being built |
| ⚠️ | **action needed** — built and tested, waiting for Ron to verify; or returned with a comment; or blocked on an answer from Ron. The row says what the action is |
| ✅ | done — built, tested, **and verified by Ron** |

Claude never marks ✅ on its own.

---

## Phase 0 — the repository ⬜

| # | Task | Done when |
|---|---|---|
| 0.1 | `git init`, GitHub repo `reaglin/MySocialPlus` (private), `.gitignore` (bin, obj, manual-test) | First commit pushed |
| 0.2 | `MySocialPlus.slnx`, `Directory.Build.props` (`MySocialPlusVersion`, nullable, TreatWarningsAsErrors) | `dotnet build` clean |
| 0.3 | `MySocialPlus.Core` (net10.0), `MySocialPlus.App` (net10.0-windows, WPF), xUnit tests | `dotnet test` green |
| 0.4 | AI Manager packages built on this machine (`..\AiManager\build\pack.ps1`) and `Eaglin.AiManager` + `.Wpf` 1.6.0 referenced | The app builds against the feed |

## Phase 1 — the model and the store ⬜

| # | Task | Done when |
|---|---|---|
| 1.1 | Tree model: topics, people, accounts, posts; the two roots My Posts and My Friends | Tests cover add / move / rename / delete |
| 1.2 | `tree.json` read and write; unknown fields kept | Round-trip test |
| 1.3 | Item cache (SQLite): items keyed by network + id, newest-first queries per node | Tests with fixture items |
| 1.4 | Seen state and badge roll-up | A test: three new items under a person show 3 on the person, the topic and the root; opening the person clears all three |
| 1.5 | Sample data (`--write-sample`) so the window can be built before any network is connected | The sample opens with badges |

## Phase 2 — the window ⬜

| # | Task | Done when |
|---|---|---|
| 2.1 | MainWindow: tree left, content right, splitter; badges on nodes | Driven through UI Automation on the sample |
| 2.2 | Content pane: merged time-ordered list for a topic/person; a post with its comments | Shows network, author (or "name hidden by Facebook"), time, text, picture, link out |
| 2.3 | Manage the tree: new topic, add person, add an account to a person, drag a person between topics | Hand-test |
| 2.4 | Newest-first / oldest-unread-first toggle | Remembered per node |
| 2.5 | A first-run screen that says what the program is and what to do first (connect a network) | UX review |
| 2.6 | UX: cognitive walkthrough + heuristic evaluation (`ux-reviewer`) | Findings fixed or recorded |

## Phase 3 — Bluesky (the first real network) ⬜

| # | Task | Done when |
|---|---|---|
| 3.1 | Sign in with a Bluesky **app password** (DPAPI, `%LOCALAPPDATA%`) | Token window: paste, check, save, remove |
| 3.2 | My Posts ← Ron's posts, with replies, likes and reposts as activity | Fixture tests + a real run |
| 3.3 | My Friends ← import the people Ron follows (he picks which, and into which topics) | Hand-test |
| 3.4 | Poller: interval while the app is open, back-off on rate limit, "last checked" shown | Tests with a fake clock |

## Phase 4 — Facebook (Ron's own posts) ⬜

Ron's second network (2026-10-11). Personal use, so the Meta app stays in Development mode.

| # | Task | Done when |
|---|---|---|
| 4.1 | **Check first:** Graph API Explorer — Ron's recent posts and the comments on one; record exactly which fields come back (is `from` hidden?) | Results written into `docs/PLAN.md` |
| 4.2 | Meta developer app in Development mode, Ron as admin; sign-in (OAuth in WebView2), long-lived token in DPAPI | Token refresh tested |
| 4.3 | My Posts ← Facebook posts with comment and reaction counts (and names where Meta gives them) | Real run |
| 4.4 | Facebook friends as "profile only" people: the node opens their profile in WebView2, no badge, and says why | Hand-test |
| 4.5 | Facebook Page (if Ron wants one in the tree — open question 3) | Real run |

## Phase 5 — the other open sources (later, as Ron wants them) ⬜

| # | Task | Done when |
|---|---|---|
| 5.1 | RSS/Atom (blogs, Substack) | Fixture tests |
| 5.2 | YouTube channels via RSS (no key); comments on Ron's videos via Data API (optional key) | Fixture tests |
| 5.3 | Mastodon (any instance, OAuth) | Fixture tests + a real run if Ron has an account |
| 5.4 | Reddit (users and subreddits) | Fixture tests |
| 5.5 | Instagram (only with a Creator/Business account): posts + comments | Real run |

## Phase 6 — the AI ⬜

| # | Task | Done when |
|---|---|---|
| 6.1 | AI Settings from `Eaglin.AiManager.Wpf`; calls tagged `MySocialPlus` | Opens, uses Ron's keys |
| 6.2 | The asks Ron chooses (open question 6) | Each one proposes; nothing posts |

## Phase 7 — the finish ⬜

| # | Task | Done when |
|---|---|---|
| 7.1 | About: "Created by Dr. Ron Eaglin" card + softwareplus.ai link | Ron looks |
| 7.2 | Icon with the "+" | Ron looks |
| 7.3 | `docs/MANUAL-TESTING.md` and a `manual-test\` build | Ron hand-tests |
| 7.4 | ~~Store package~~ ❌ not doing — personal use (Ron, 2026-10-11) | — |

---

## Open questions for Ron (asked 2026-10-11)

1. ~~**Which networks matter most?**~~ **Answered 2026-10-11: Bluesky and Facebook, mostly.**
   Bluesky is phase 3, Facebook phase 4; the other open sources moved to phase 5, as wanted.
2. ~~**Just for Ron, or a Store program for others?**~~ **Answered 2026-10-11: personal use.** The
   Meta app stays in Development mode with Ron as admin — no App Review. No Store package (7.4 ❌).
3. **Facebook: personal profile, a Page, or both?** (Personal profile assumed from 2026-10-11; a Page
   is task 4.5 if wanted.) Instagram is not in use, so it waits in phase 5.
4. **How often should it check?** Proposed: every 15 minutes while the program is open; no background
   service at first.
5. ~~**The name.**~~ **Settled by question 2:** "MySocial+" stands; nothing to reserve, since it is not going to the Store.
6. **What should the AI do?** Proposed in `docs/PLAN.md`: catch-up summaries, sort friends into
   topics, draft replies (Ron sends), search the cache. Which, and anything else?

---

## References

| Document | What it is for |
|---|---|
| `docs/PLAN.md` | What each network allows, the decisions, the architecture |
| `CLAUDE.md` | Rules for working in this repo |
| `..\CLAUDE.md` | Portfolio rules (marks, development cycle, About card, "+" names) |
| `..\AiManager\CLAUDE.md` | How to consume the AI layer |
