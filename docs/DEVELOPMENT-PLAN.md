# MySocial+ — development plan

Numbered tasks per phase, each with what "done" means. **Started 2026-10-11; phases 0–3 built the same day** (Bluesky, on the sample and against the real public API) — Ron's hand-tests 1–5 in `docs/MANUAL-TESTING.md` are next.
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

## Phase 0 — the repository

| # | Task | Done when |
|---|---|---|
| 0.1 | ⚠️ `git init`, GitHub repo `reaglin/MySocialPlus` (**public**, Ron 2026-10-11), `.gitignore` (bin, obj, manual-test, shots) | Pushed 2026-10-11 |
| 0.2 | ⚠️ `MySocialPlus.slnx`, `Directory.Build.props` (`MySocialPlusVersion` 0.1.0, nullable, TreatWarningsAsErrors) | `dotnet build` clean — built 2026-10-11 |
| 0.3 | ⚠️ `MySocialPlus.Core` (net10.0), `MySocialPlus.App` (net10.0-windows, WPF), xUnit tests | `dotnet test` green — 33 tests, plus 1 live test run with `MSP_LIVE=1` |
| 0.4 | ⚠️ `Eaglin.AiManager` + `.Wpf` **1.6.0** referenced from `C:\nuget-local` | The app builds against the feed — built 2026-10-11 |

## Phase 1 — the model and the store

| # | Task | Done when |
|---|---|---|
| 1.1 | ⚠️ Tree model (`SocialTree`, `TreeNode`, `Account`): the two roots, topics, people, accounts; the rules (people under My Friends, accounts in people or under My Posts, one node per account). Posts are not tree nodes — they are cache items listed in the content pane | `TreeTests`: add / move / rename / delete and the rules |
| 1.2 | ⚠️ `tree.json` read and write, written through a temp file; unknown fields kept | Round-trip test |
| 1.3 | ⚠️ Item cache (SQLite `items.db`): items keyed by **account + id** (the same post can be Ron's comment and a friend's post), newest-first queries | `CacheAndBadgeTests` |
| 1.4 | ⚠️ Seen state per item and badge roll-up; an account's first read arrives as seen, so connecting does not light up old posts | Plan's test, word for word, passes; opening the person clears all three |
| 1.5 | ⚠️ Sample data: `--sample` (opens a fresh sample in %TEMP%, reads nothing, saves nothing) and `--write-sample <folder>` | The sample opens with badges (`SyncTests`, and the window) |

## Phase 2 — the window

| # | Task | Done when |
|---|---|---|
| 2.1 | ⚠️ MainWindow: tree left, content right, splitter; badges on nodes, summed up the tree; total in the title bar | Driven through UI Automation on the sample (`tools/tour-window.ps1`) — Ron's hand-test 1 |
| 2.2 | ⚠️ Content pane: a post with its replies, likes, reposts under it (oldest first, like a conversation); follows and mentions on their own; new ones shaded with "3 new" and dots; picture, link card or quote, counts, **Open on Bluesky**, **Reply on Bluesky** | Hand-test 1 |
| 2.3 | ⚠️ Manage the tree: New topic, Add person (with a first account), Add an account to a person, Rename (F2), Delete (Del), drag a person or topic onto a topic, right-click ▸ Move to | Hand-test 1 |
| 2.4 | ⚠️ Newest-first / oldest-unread-first, remembered per node (in tree.json) | Hand-test 1 |
| 2.5 | ⚠️ First run: **Welcome to MySocial+** with three steps (connect Bluesky, bring in people, make topics) and what Facebook allows; stays up until people are added; Help ▸ Getting started brings it back | UX review — Ron's hand-test 2 |
| 2.6 | ⚠️ **What's new** (added 2026-10-11, from the first look at the window): the window opens on a list of everything with something new and marks nothing seen. Opening a node marks it seen only after a moment on it, so arrow keys through the tree clear nothing; **Mark as new again** undoes it | Hand-test 1 |
| 2.7 | ⚠️ UX: cognitive walkthrough + heuristic evaluation (2026-10-11; run as a general-purpose agent with the ux-reviewer brief — no `ux-reviewer` agent in this session) | **16 findings, 13 fixed:** sign-in-again for a rejected app password; problems shown on What's new and every page with Try now / Sign in again; ⚠ on the person, not just the hidden account; seen only after a pause + Mark as new again; Getting started stays until people are added and sign-in leads to step 2 (Done is the default after success); topics made in the import window are saved even on Cancel; right words for a person with no accounts; Reply on Bluesky with its tooltip; Mastodon needs a full address; Delete says it forgets the app password; "Open Facebook profile"; Order hidden where there is nothing to order; profile-only nodes less faded. **Recorded, not done:** undo for a move (13) and a visible Move-to button (14) — right-click ▸ Move to and drag do it today; profile-only contrast (16) to be measured |
| 2.8 | Undo a move ("Moved Sarah into Family. Undo") — UX finding 13 | Hand-test |

## Phase 3 — Bluesky (the first real network)

| # | Task | Done when |
|---|---|---|
| 3.1 | ⚠️ Sign in with a Bluesky **app password**, checked by signing in before it is saved (DPAPI, `%LOCALAPPDATA%\MySocialPlus`); the window explains how to make one, shows the saved one masked, and removes it | Ron's hand-test 2 |
| 3.2 | ⚠️ My Posts ← Ron's posts (`getAuthorFeed`) + notifications (replies, likes, reposts, quotes, mentions, follows); posts the activity is about are fetched if older; the session refreshes itself, and signs in again from the app password | Fixture tests (saved real answers + a lexicon-shaped notifications file) — **real run with Ron's account is hand-test 4** |
| 3.3 | ⚠️ My Friends ← Ron ticks people from everyone he follows (all pages), finds by name, picks or makes the topic. Friends are read from the public API, no sign-in needed | Live test (`MSP_LIVE=1`) reads a real account and pages a 700-follow list; hand-test 3 |
| 3.4 | ⚠️ Poller: every 15 minutes while open (open question 4), back-off doubling to 2 h on a failure or "slow down", never sooner than Bluesky asks; Check now / F5; next check and last checked shown | `PollScheduleTests` (times given, no clock); hand-test 5 |

## Phase 8 — what to remember about friends, My Events, My Things (added 2026-10-11)

Ron, 2026-10-11: the program works **with or without social media**; two more headings, **My Events**
and **My Things**; and for people and things **a flexible structure** — name/value pairs that grow
without changing code. Numbered 8 so the phases above keep their numbers. Built the same day.

| # | Task | Done when |
|---|---|---|
| 8.1 | ⚠️ Four headings: My Posts, My Friends, **My Events**, **My Things**; an older tree.json gets the new two; topics keep to their own heading | `EventsAndThingsTests` |
| 8.2 | ⚠️ **Details**: any number of name/value pairs on a person, event or thing (text, note, date, birthday, number, money, link, phone, email); rename or remove any; "Remind me" on dates | Tests; hand-test 6 |
| 8.3 | ⚠️ **Kinds as data**: `templates.json` (Friend, Family member, Colleague; Concert, Race, **Adventure race**, Yoga or fitness class, Dance, Trip…; Car, House, Coffee maker, Computer, Furniture, Appliance, Bicycle, Boat, Document, Subscription… 38 in all), written on first run and Ron's to edit; **Save these details as a new kind…** | Tests (shipped set, Ron's file kept, newer kinds added); hand-test 6 |
| 8.4 | ⚠️ **Log**: dated entries with an optional cost (service history, repairs, notes from a talk), with a total | Hand-test 8 |
| 8.5 | ⚠️ **Events**: when (date, time, all day, end), where, repeats (week, month, year), "Me" (thinking about it, going, went, didn't go); **Add to my calendar** (.ics) | Tests (repeat, .ics); hand-test 7 |
| 8.6 | ⚠️ **Going with**: friends from the tree or any name, **Add as a new friend**; each with their email and phone; **Email everyone**, **Copy emails**, **Copy phone numbers** (for a group text); says whose email or phone is missing | Tests (`Contacts`); hand-test 7 |
| 8.7 | ⚠️ **Agenda**: timed steps (check-in, start, cut-offs…), in time order, in the calendar file too | Tests; hand-test 7 |
| 8.8 | ⚠️ **Coming up** on What's new: events, birthdays (with the age), Remind-me dates for 30 days, and overdue ones for a week | Tests; hand-test 6 |
| 8.9 | ⚠️ **A friend's page**: big buttons — **Open Facebook profile** (inside the program), **Open Google Photos** (any link detail), **Email**, **Add a social media profile…**; a pasted address like `www.facebook.com/name` becomes the handle; Facebook is the first choice when adding someone | Tests (`NormalizeHandle`); hand-test 6 |
| 8.10 | ⚠️ **My Events / My Things pages**: upcoming and past events, things by topic, New event… / New thing… / New topic…; the Add button under the tree follows the heading | Hand-tests 7–8 |
| 8.11 | ⚠️ **Getting started without social media**: a friend, an event, a thing; Bluesky is optional | Hand-test 6 |
| 8.12 | ⚠️ UX: walkthrough + heuristics on the new pages (2026-10-11, the four real use cases) | **17 findings, 15 fixed:** Remove buttons say Remove and ask first when something would be lost; **Add someone else…** takes name, email, phone and "also add to My Friends"; **Add email or phone…** beside a teammate missing them; edits saved on closing; the link bar updates as soon as a link or email is typed; a thing's history comes before its details, and empty suggested details fold behind "Show N empty details"; a bad agenda time is reported; a hint under Coming up on clearing reminders; Open Facebook profile on the Posts tab too; "profile" wording throughout; odd Facebook addresses (people/…, share/…) kept whole; Kind cannot be cleared to nothing; "Am I going?"; the Facebook explanation grey, not warning yellow; New topic tooltip; a short footer; Add to my calendar waits for a date. **Recorded:** 8.15, 8.16 |
| 8.15 | Edit a log entry or an agenda step in place (today: remove and add again) — UX finding 6 | Hand-test |
| 8.16 | A **Done** / **Next date…** button on an overdue reminder under Coming up — UX finding 8 | Hand-test |
| 8.13 | Export a person's or an event's contacts as vCard, if Ron wants them on his phone | — idea, waits for use |
| 8.14 | Photos or receipts attached to a thing (a picture of the VIN plate, the warranty PDF) | — idea, waits for use |

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
| 6.1 | ⚠️ AI Settings from `Eaglin.AiManager.Wpf` (Tools ▸ AI Settings…, opened for the app `MySocialPlus`); nothing calls the AI yet | Opens, uses Ron's keys — Ron to look |
| 6.2 | The asks Ron chooses (open question 6) | Each one proposes; nothing posts |

## Phase 7 — the finish ⬜

| # | Task | Done when |
|---|---|---|
| 7.1 | ⚠️ About: "Created by Dr. Ron Eaglin" card + softwareplus.ai link (to the home page: MySocial+ has no page there, being personal) — built 2026-10-11 | Ron looks |
| 7.2 | Icon with the "+" | Ron looks |
| 7.3 | ⚠️ `docs/MANUAL-TESTING.md` (tests 1–5) and the `manual-test\` build — built 2026-10-11 | Ron hand-tests |
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
   service at first. (Built that way 2026-10-11 — say if it should change.)
5. ~~**The name.**~~ **Settled by question 2:** "MySocial+" stands; nothing to reserve, since it is not going to the Store.
6. **What should the AI do?** Proposed in `docs/PLAN.md`: catch-up summaries, sort friends into
   topics, draft replies (Ron sends), search the cache. Which, and anything else?

---

## References

| Document | What it is for |
|---|---|
| `docs/PLAN.md` | What each network allows, the decisions, the architecture |
| `docs/MANUAL-TESTING.md` | Ron's hand-tests, and the command that builds `manual-test\MySocialPlus.exe` |
| `tools/tour-window.ps1` | Opens the sample, clicks through the tree with UI Automation and photographs each view |
| `CLAUDE.md` | Rules for working in this repo |
| `..\CLAUDE.md` | Portfolio rules (marks, development cycle, About card, "+" names) |
| `..\AiManager\CLAUDE.md` | How to consume the AI layer |
