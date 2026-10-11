# MySocial+ — hand tests

Each test says what to do and what should happen. Report anything that differs, or anything that
was confusing, and it becomes a plan item.

**The build to use:** `manual-test\MySocialPlus.exe` (rebuilt with the command below), or
`dotnet run --project src/MySocialPlus.App`.

```powershell
dotnet publish src/MySocialPlus.App/MySocialPlus.App.csproj -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o manual-test
```

**Your data** is in `Documents\MySocialPlus` (`tree.json`, your tree; `items.db`, the saved posts).
The app password is in `%LOCALAPPDATA%\MySocialPlus`, encrypted. To start over, close the program and
delete both folders.

---

## Phases 0–3 — the tree, the window, and Bluesky (2026-10-11)

### Test 1 — look around the sample (no network)

1. Open a PowerShell in the repo and run `manual-test\MySocialPlus.exe --sample`. The title says
   **sample data**; nothing is read from the networks and nothing is saved.
2. The window opens on **What's new**: six rows (My Posts ▸ Bluesky with 4, then five friends with 1
   each). The tree shows the same numbers, summed up: Family 2, Colleagues 3, My Friends 5.
3. Click **My Posts ▸ Bluesky**. Your post about the EGN3443 video is at the top, shaded orange with
   **3 new**: Tom's like, Sarah's reply and Dana's repost have orange dots; Priya's older reply does not.
   "Lena Fischer followed you" is its own card. After about a second the numbers in the tree go away and
   a line says **Marked 4 items here as seen** with **Mark as new again**.
4. Press **Mark as new again**: the 4 comes back on Bluesky and My Posts.
5. Click **Sarah Lee**: her garden-path post is new; a yellow line says she is also on Facebook, with
   **Open Facebook profile** (opens facebook.com inside the program — her sample handle is made up, so
   Facebook will say the page does not exist).
6. Click **Jim Halloran** (Facebook only): no posts, and the line says why there is no number.
7. Use the arrow keys to move quickly down the tree through Colleagues and its people. The numbers
   should **not** disappear while you pass through; they clear only where you stop for a moment.
8. Drag **Ann Chu** onto **Family**. She moves. Right-click a person: **Move to**, **Rename…**,
   **Delete**, **Add an account to this person…**.
9. **Order: Oldest unread first** on My Friends puts the unread posts first, oldest at the top.

### Test 2 — connect your Bluesky account

1. Run `manual-test\MySocialPlus.exe` (no `--sample`). First time: **Welcome to MySocial+** with three
   steps.
2. Press **Connect Bluesky…**. Follow the window: make an app password in Bluesky (Settings ▸ Privacy
   and security ▸ App passwords ▸ Add App Password, name it MySocial+, leave direct messages unticked),
   type your handle, paste it, **Check and save**.
3. It should say **Signed in as @…** and offer **Choose people I follow…** (and Done).
4. Try a wrong app password once first: it should say Bluesky did not accept it and how to make a new one.

### Test 3 — bring in people and sort them

1. Press **Choose people I follow…** (or Accounts ▸ Choose people I follow on Bluesky…). Everyone you
   follow is listed with their handle; **Find** narrows the list.
2. **New topic…** → Family. Tick two people, **Add 2 people**. They appear under Family.
3. Within a minute the status bar says **Checked N accounts**. Their posts appear when you open them —
   **without** numbers: what was already there when you added someone is not "new". New posts after
   this get a number.
4. Add someone by hand: select a topic, **Add person**, type a name, Network Facebook, their profile
   address. They appear with a link icon and no number; opening them says why.

### Test 4 — your own posts

1. Open **My Posts ▸ Bluesky**: your newest posts, with the replies, likes and reposts on them. Your
   own posts are never counted as new.
2. Ask someone to reply to one of your posts (or reply from another account). Within 15 minutes —
   or press **Check now** (F5) — My Posts gets a number. Open it: the reply is under the post, with
   **Reply on Bluesky**, which opens it in your browser. MySocial+ never posts for you.

### Test 5 — when things go wrong

1. Unplug the network (or turn off Wi-Fi) and press **Check now**. What's new and every page should
   say the accounts could not be read, when it will try again, and offer **Try now**. People whose
   accounts failed show a ⚠ in the tree.
2. In Bluesky, delete the MySocial+ app password, then **Check now**: a line says Bluesky no longer
   accepts the app password, with **Sign in again…**.

**Tell Claude:** anything that confused you, anything that should be worded differently, and whether
15 minutes between checks is right (open question 4).

---

## Phase 8 — friends' details, My Events, My Things (2026-10-11)

None of these need social media. Try them on the sample first (`--sample`), then with your own data.

### Test 6 — a friend: one click to their Facebook profile, and things worth remembering

1. **Add person** (under the tree). Type the name, Kind **Friend**, Network **Facebook**, and paste the
   address of their profile as you copied it from the browser (`www.facebook.com/…` — with or without
   `https://`). **Add person**.
2. Their page opens on **About**: a big **Open Facebook profile** button at the top. Press it: their
   profile opens inside MySocial+ (sign in to Facebook there the first time).
3. Fill in a **Birthday** like `3/12` (no year) or `3/12/1984`; it should show as "March 12" or
   "March 12, 1984". Add **Children**, **Likes**.
4. **Google Photos**: in Google Photos open People & pets, pick them, copy the address from the browser,
   and paste it into the **Google Photos** detail. Click elsewhere — an **Open Google Photos** button
   appears at the top of their page.
5. **Add a detail**: type any name ("Shoe size", "Favorite restaurant") and press Add detail.
6. On the sample, **What's new ▸ Coming up** lists Sarah Lee's birthday ("turns 42") and Manny Otero's.

### Test 7 — an event with a team (your adventure race)

1. Select **My Events**, press **New event…**: name it, Kind **Adventure race**, the date and a time.
2. On its page: **Where**, **Ends** (the next day), Me: **Going**.
3. **Going with**: pick a friend from the list and **Add**; type a teammate who is not in My Friends yet
   and press **Add as a new friend** — they now appear under My Friends too. Fill in their Email and
   Phone on their page (click their name).
4. **Agenda**: add the steps (check-in, pre-race meeting, start, cut-offs) with dates and times; they
   sort themselves into time order.
5. **Email everyone** opens your mail program with every address. **Copy phone numbers** — paste them
   into a group text on your phone or in Phone Link.
6. **Add to my calendar…** opens the event in Outlook (or your calendar), with the team and agenda in it.
7. On the sample, open **Nationals Adventure Race** to see all of this filled in.

### Test 8 — a thing (your car)

1. Select **My Things**, **New thing…**: name it, Kind **Car**.
2. Fill in Year, Make, Model, License plate. Set **Registration renews** to a date in the next month —
   **Remind me** is already ticked — then press **What's new**: it is under **Coming up**.
3. **Service history**: add an oil change with today's date and a cost; the total shows under it.
4. **Save these details as a new kind…** on something unusual (a kayak): next time, New thing… offers
   **Kayak** with the same detail names, empty.
5. File ▸ Open the data folder: `templates.json` is there — the list of kinds, which you can edit.
