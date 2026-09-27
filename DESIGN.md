# SoDArchipelago design

The agreed randomizer design for Shape of Dreams. It was settled in a design review on 2026-09-27 and replaces the
original "achievement reward = item" scaffold. Where the code and this document disagree, this document wins.
Items marked **(verify)** are assumptions that need checking in-game or in decompiled code before release.

Nothing has been released yet, so item/location IDs can still change. Once a version is released they are frozen
(see AGENTS.md conventions).

## Locations (228)

| Kind | Count | Progress type | Notes |
|---|---|---|---|
| Achievements (`ACH_*`) | 93 | default | Every achievement in `RawData/en-US/achievements.json`. None are excluded. |
| World clears | 135 | **priority** | 5 worlds × 3 difficulties × 9 Travelers. These are the only priority locations. |

- **World clear** = the Traveler you are playing kills that world's final boss, or moves on to the next world.
  Use whichever is easier to detect reliably. *Built:* moving on (`ZoneManager.ClientEvent_OnZoneLoaded`, traveling, not
  loading a save). On arrival `currentZoneIndex` equals the number of the world just left (it starts at -1, is 0 in
  world 1 and keeps counting through loops). Leaving world 5 is always a zone change: into `Zone_Primus` (the Pure White
  Dream ending, through `Rift_Sidetrack_TheDream`) or into the next loop.
- **Difficulties:** Deep Sleep, Ominous Dream, Nightmare. Nap has no locations.
- **Cumulative:** a clear on a higher difficulty also sends the same world's checks for every lower difficulty
  (Nightmare sends Nightmare, Ominous Dream and Deep Sleep).
- **Loops** past world 5 send nothing. Limbo (`diffLimbo`) is its own difficulty id, which isn't one of the four
  mapped difficulties, so it sends no world clears and its wins don't count for the goal *(resolved from code)*.
- **Boss-kill locations are removed.** They duplicated world clears and achievements.
- **Naming:** `Achievement: <display name>` (unchanged) and `World <n> Clear (<Difficulty>): <Traveler>`, for example
  `World 3 Clear (Nightmare): Mist`.
- **Location groups** (for players' own `exclude_locations` / `priority_locations`): `Achievements`,
  `World Clears`, one group per Traveler (their achievements and world clears), one per difficulty, and
  `Build-Dependent Achievements` (the ones that need specific Memories: Four of a Kind, Fight Fire with Fire,
  Hotter Fire Wins, Omega Point, Wrist-Friendly Build, Twinkle Twinkle, Support Specialist, Now Now Stay Still,
  Master of Mystic Arts).

## Items

In vanilla each achievement unlocks exactly one thing, which gives 93 unlocks. All 93 are items, grouped as follows.

| Class | Item | Copies | Effect |
|---|---|---|---|
| progression | `Progressive <Traveler>` for Aurena, Bismuth, Cetus, Nachia, Shell, Vesper, Yubar | 4 each (28) | 1st copy unlocks the Traveler; copies 2–4 unlock their 3 alternate memories in the fixed order below |
| progression | `Progressive Lacerta`, `Progressive Mist` | 3 each (6) | They start unlocked, so every copy is an alternate memory |
| useful | `Memory: <name>` (general, non-Traveler Memories locked behind achievements) | 15 | Makes the Memory able to drop in runs |
| useful | `Essence: <name>` | 29 | Makes the Essence able to drop in runs |
| useful | `Lucid Dream: <name>` | 15 | Unlocks the Lucid Dream modifier |
| filler | `Mastery: <Traveler>` (one item per Traveler) | 7 per Traveler (63) | +5 mastery levels to that Traveler |
| filler | `Stardust` | the rest (72) | +650 Stardust |

- **No traps.**
- **Déjà vu is untouched.** It's the Pure White Dream carry-over system that costs Stardust.
- **Counts:** 34 progression + 59 useful + 63 mastery + 72 Stardust = 228, equal to the location count.
- **Filler targets:** filler alone gives exactly **35 mastery on every Traveler** (the highest mastery any star or
  star slot requires) and **46,800 Stardust**. That covers the 45,275 needed for every star level, plus a buffer for
  star slot unlocks (40 + 20 × n Stardust per extra slot, where n counts from 0 past the Traveler's default slot count
  for that star type). **(verify)** The mod logs each Traveler's star slot counts and their total cost at startup
  (`[AP] Star slots ...`); check the 1,525 buffer against it.
- Vanilla run rewards (Stardust, mastery points) are not counted toward these targets and stay as they are.

### Alternate memory order

Each Traveler's three locked memories are one per slot (Bismuth's are all QR). Progressive copies unlock them in this
order: **Q → R → Identity**.

| Traveler | Copy 1 | Copy 2 | Copy 3 | Copy 4 |
|---|---|---|---|---|
| Aurena | Traveler | Reduction (Q) | Chain Reaction (R) | Beautiful Threat (Identity) |
| Bismuth | Traveler | Distorted Mind | Tales of Hellfire | Valiant Heart |
| Cetus | Traveler | Big Boreal Chunk (Q) | Teaching Manners (R) | Charged Anguillian (Identity) |
| Nachia | Traveler | Moonlight Pact (Q) | Serpent's Blessing (R) | Circle of Life (Identity) |
| Shell (`Hero_Husk`) | Traveler | Death Mark (Q) | Deception (R) | Scar of the Wind (Identity) |
| Vesper | Traveler | Discipline (Q) | Baptism of the Sun (R) | El's Mercy (Identity) |
| Yubar | Traveler | Supernova (Q) | Tranquility (R) | Converging Stars (Identity) |
| Lacerta | — | Incendiary Rounds (Q) | Precision Shot (R) | Double Tap (Identity) |
| Mist | — | Flèche (Q) | Parry (R) | Astrid's Masterpiece - Priorité (Identity) |

The order lives in one data table in the extractor. It is not derived from anything.

## Logic (access rules)

"Copies" means how many of that Traveler's progressive item you have received.

| Location | Locked Travelers need | Lacerta / Mist need |
|---|---|---|
| Achievement that requires a Traveler (e.g. "as Aurena") | 1 copy | nothing |
| Any other achievement | nothing | nothing |
| World clear, Deep Sleep | 1 copy | nothing |
| World clear, Ominous Dream | 2 copies (Traveler + 1 memory) | 1 copy |
| World clear, Nightmare | 3 copies (Traveler + 2 memories) | 2 copies |
| Goal | `goal_traveler_count` Travelers unlocked (Lacerta and Mist count). **No memory requirement.** | |

- Nothing requires a Traveler's third memory. It stays progression, but the logic never waits on it.
- **Achievement → Traveler mapping:** use a hand-maintained table or parse "as <Traveler>" from the description.
  Don't infer it from the reward. *Resolved:* the extractor's hand table (27 achievements, 3 per Traveler) matches both
  the descriptions ("as/with/using <Traveler>") and the `Hero_*` types each `ACH_*` class references in `Dew.Contents`.
  The only other class that references a Traveler, `ACH_ANGER_MANAGEMENT_PROFESSIONAL` (Vesper), isn't in RawData and
  isn't a location.

## Goal

Win a run at `goal_difficulty` **or harder** with `goal_traveler_count` **different** Travelers.

- "Win" means reaching either ending: `ResultType.PureWhiteDream` or `ResultType.StarlessPath`.
- `UnknownFate` doesn't count *(resolved from code)*: only demo/booth builds use it, to end the run at the end of the
  demo's content (`PlayGameManager.LoadNextZone`). The retail build never produces it.
- Wins are recorded in the bound profile (Traveler + difficulty id), so a win played offline counts on the next connect.
- The mod records in the bound profile which Travelers have counted. It sends `ClientGoal` when the count is reached.

## Options (YAML)

| Option | Type | Default | Notes |
|---|---|---|---|
| `goal_difficulty` | choice: `deep_sleep`, `ominous_dream`, `nightmare` | `nightmare` | |
| `goal_traveler_count` | range 1–9 | 9 | |
| `mastery_packs_per_traveler` | range 0–15 | 7 | 0 removes mastery from the pool. 15 × 9 = 135 fills every filler slot. |
| `mastery_pack_value` | range 1–35 | 5 | levels per pack |
| `stardust_pack_value` | range 1–10,000 | 650 | Stardust fills every remaining filler slot, so it can't be 0 |
| `death_link` | toggle | off | |

Remove `boss_locations` and `travelers_required_for_goal`. `slot_data` must carry everything the mod needs: goal
settings, pack values, death_link, and the data version.

## Client mod behavior

### Checks
- **Achievements:** when an achievement completes, send its check. **Suppress the vanilla unlock reward**, since the
  AP item replaces it. **Keep** the vanilla Stardust bonus (`DewAchievementItem.grantedStardust`, usually 50).
- **World clears:** detect the clear, then record it in the bound profile. Send that world's check for the current
  difficulty and every lower one, for the Traveler being played.
- **On every connect,** rebuild the full check list from the profile (completed achievements plus recorded world
  clears) and resend all of it. The server ignores duplicates.

### Received items
- Unlocks go through the game's own functions: `DewProfile.UnlockHero`, `UnlockSkill`, `UnlockGem`,
  `UnlockLucidDream`.
- Unlocks are **rebuilt from the full received-item list** on every connect. Receiving one twice changes nothing,
  so no item index is needed.
- **Filler** (Stardust to `DewProfile.stardust`, mastery points to `DewProfileStats`) needs a per-type **applied
  counter** stored in the profile. Store it so it is saved together with the currency it protects. Mastery lives in
  the separate `stats` file, so its counter has to be saved in that same write, or be made crash-safe some other way.
  Items that arrive several times, and server/cheat items (location −1/−2), count normally.
- **Timing mirrors vanilla.** Everything is written to the profile right away. Things that can only change between
  runs take effect then: the loot pool is built at run start, and Traveler, loadout and Lucid Dreams are chosen in the
  lobby. The notification shows "(next run)" when that applies.
- Mastery arrives as levels but the game stores points. Convert with `Dew.GetRequiredMasteryPointsToLevelUp`.
  *Resolved from code:* there is no mastery level cap. The per-level cost table clamps, so every level from 30 on costs
  30,600 points; mastery level-up Stardust rewards stop at level 30. The mod adds the exact points for the next N levels,
  so partial progress is kept.
- **Where the records live.** New JSON fields would be dropped on the game's next save, so AP data goes into string
  lists the game only reads with `Contains`: `DewProfile.experienceFlags` holds the binding marker, the unlock record
  (`AP:unlock:<target>`), world clears (`AP:clear:<location key>`), wins (`AP:win:<Traveler>:<difficulty id>`) and the
  Stardust counter (`AP:applied:STARDUST=<n>`). The mastery counters (`AP:applied:MASTERY_<Traveler>=<n>`) live in
  `DewProfileStats.recoveredLossPoints`, in the `stats` file, so each is written in the same save as the value it
  protects.
- **The unlock record.** `DewProfile.Validate` runs on every profile load and re-derives unlocks from achievements: it
  locks the target of every incomplete achievement and unlocks the target of every completed one. `UnlockHero` also
  unlocks a Traveler's alternate memories whose achievement is complete. On a marked profile the mod therefore keeps
  its own record of what AP has granted, and after `Validate`, on mod load, on connect and after items it sets every
  managed unlock (the 93 targets, plus a locked Traveler's own memories) to match, using the game's `Unlock*`/`Lock*`
  functions. This works offline too.

### Save profile binding
Every AP seed/slot gets its own game profile. Your normal save must never be read or written by AP logic.

1. The player creates a new profile in the game's own UI.
2. They connect from the title screen and confirm "bind this profile to <seed>/<slot>".
3. The mod refuses to bind a profile that isn't fresh: it must have no completed achievements and no runs played. There
   is also a deliberate override for recovery (`ap_bind_force`). *Built test:* no completed achievements, no recorded
   game results (`lastGameResults`, `recentlyConcededGames`) and a total Traveler play count of 0. **(verify)** Whether
   the tutorial run makes a new profile fail it; the mod logs the numbers.
4. On bind, write a marker into `DewProfile.experienceFlags`, e.g. `Archipelago:<seed>:<slot>`. It's a free-form
  string list that the game only reads with `Contains`. The marker travels with the profile (Steam Cloud, moves)
  and can't get separated from it.
5. **Seed check before login.** `ConnectAsync` returns `RoomState.Seed`. If it doesn't match the loaded profile's
   marker, refuse and don't log in.
6. **A single guard before every action** (send, apply, suppress, gate): the loaded profile carries the marker for
   the current session's seed/slot, and `DewSave.profileMainPath` is not null (the Transient profile has a null
   path). Hook `DewSave.LoadProfile` and `DewSave.CreateProfile` to re-check. On a switch, disconnect, and drop queued
   work that was tagged with the old profile.
7. A **marked profile** always gets AP behavior (reward suppression, gating), **even offline**. An **unmarked
   profile** always gets pure vanilla behavior.
8. **Block the Steam achievement sync on marked profiles** (`DewSave.SyncAchievements` and the
   `SteamUserStats` calls in `AchievementManager.CompleteAchievement`). The fresh profile would otherwise
   overwrite Steam's stat progress with lower values. *Built:* `SyncAchievements` is skipped, and
   `SteamUserStats.SetStat`/`SetAchievement` are blocked while a marked profile is loaded. That also covers the other
   two places that push the same lower stats (`AchievementManager.SetPlatformStats`,
   `DewAchievementItem.FlushProgressToProfile`).
9. Connection details (host:port, slot, password) are stored as **editable settings**, separate from identity.
   archipelago.gg rooms can change port.

Useful facts: the game loads the last-used profile at **startup** (title screen), before "Dream Alone" / "Dream
Together". Profiles are only switched, created or deleted from the title screen's profile selection. Mods are
enabled for every profile, not per profile (`platformSettings.activeMods`). Achievements are stored per profile
(`DewProfile.achievements`) and only ever pushed to Steam, never read from it.

### Offline play
Allowed. A marked profile plays offline with the items it has already received. Checks are recorded in the profile
and sent on reconnect. An **always-visible "OFFLINE — checks will send on reconnect" indicator** is shown. The setup
guide explains that offline play delays other players' items.

### Co-op
No special handling. Each player's own profile, mod and slot are independent. The shared loot pool is the union of
the players' unlocks, which is vanilla co-op behavior. **(verify)** That achievement and world-clear events fire on a
joining client, not only on the host. (From code they should: achievements are tracked per client, and the
zone-loaded event is an RPC to every client.)

### DeathLink (off by default)
- **Send** when your Traveler is knocked down (`ClientEventManager.OnHeroKnockedOut` /
  `Hero.ClientHeroEvent_OnKnockedOut` for the local hero).
- **Receive:** kill your Traveler. This ends the run only in solo. In co-op it's a normal knockdown that teammates can
  revive. *Built:* `Entity.Kill()`, which goes through the hero's normal death interrupt (a knockdown, or a bleed-out
  first on difficulties with `enableBleedOuts`).
- **Known limitation (accepted 2026-09-27): a player who *joined* someone else's co-op game can't receive DeathLinks.**
  `Entity.Kill` is server-only and the game has no client command to kill your own Traveler, so the mod on a joining
  client can't apply one. It shows the DeathLink with "(can't be applied: only the host can kill Travelers)" and does
  nothing else. Joining players still *send* DeathLinks. The host and solo players receive them normally. The
  alternative (the host's copy of the mod kills the joining player's Traveler) was rejected: it needs custom network
  messages and only works when the host also runs the mod. The setup guide and game info page state the limitation.
- A knockdown caused by a received DeathLink does not send a new one.
- Each player turns it on independently. There's no co-op-specific handling.

## Open questions to settle during the build
- ~~Map the game's difficulty IDs~~ The asset catalog has `diffTutorial`, `diffEasy`, `diffNormal`, `diffHard`,
  `diffNightmare` and `diffLimbo`. The build maps Nap = `diffEasy`, Deep Sleep = `diffNormal` (the default),
  Ominous Dream = `diffHard`, Nightmare = `diffNightmare`. **(verify)** The mod logs each id's in-game name at startup
  (`[AP] Difficulty ids -> display names`); the order is an assumption until that line is checked.
- ~~World-clear detection~~ Zone transition, world = `currentZoneIndex` on arrival (see "Locations").
- Star slot counts per Traveler (the Stardust buffer): still open, logged at startup. ~~Mastery cap~~,
  ~~fresh-profile test~~ and ~~UnknownFate~~: see above.
- ~~`isAlteringGameplay`~~ Kept on.
- ~~DeathLink for a joining co-op player~~ Accepted as a known limitation (see "DeathLink").
