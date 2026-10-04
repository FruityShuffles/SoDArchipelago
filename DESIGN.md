# SoDArchipelago design

The agreed randomizer design for Shape of Dreams. It was settled in a design review on 2026-09-27 and replaces the
original "achievement reward = item" scaffold. Where the code and this document disagree, this document wins.
Item/location IDs are frozen once a version is released: the extractor keeps every key's ID through `id_history`.

## Locations (228)

| Kind | Count | Progress type | Notes |
|---|---|---|---|
| Achievements (`ACH_*`) | 93 | default | Every achievement in `RawData/en-US/achievements.json`. None are excluded. |
| World clears | 135 | **priority** | 5 worlds × 3 difficulties × 9 Travelers. These are the only priority locations. |

- **World structure** (corrected 2026-09-27). A loop has **4 normal worlds**. Beating the world 4 boss opens two rifts:
  the normal exit (next loop) and the Dream rift (`Rift_Sidetrack_TheDream`, only in the last world's boss room). The Dream
  rift travels to `Zone_Primus`, which is **world 5**: a shop, then `Shrine_PrimusDoor` (a room change inside the zone) to
  the Primus boss. When Primus dies (`Mon_Primus_BossPrimusAeron.OnDeath` → `Primus_Ending.StartPrimusDeath`, server),
  the players are teleported to the ending area (a teleport inside the same room, not a room or zone load). Interacting
  with the white light pillar plays the ending cutscene, which calls `ConcludePureWhiteDream`.
- **World clear, worlds 1–4** = moving on to the next world (`ZoneManager.ClientEvent_OnZoneLoaded`, traveling, not
  loading a save). On arrival `currentZoneIndex` equals the number of the world just left (it starts at -1, is 0 in
  world 1 and keeps counting through loops). Leaving world 4 is always a zone change: into `Zone_Primus` or into the
  next loop. Only indexes 1–4 count (index 5 is leaving loop 2's first world, not a World 5 clear).
- **World clear, world 5** = beating Primus, detected as a **Pure White Dream win** (decided 2026-09-27). *Built:* on
  `GameResultManager.ClientEvent_OnGameConcluded` with `result == ResultType.PureWhiteDream`, record and send the World 5
  clears (same hook as the Starless Path rule). That hook also fires on death (`GameOver`) and concede (`Conceded`), so
  the result type must be checked. The zone-change rule can't detect world 5: the index on arrival in `Zone_Primus` is 4
  (world 4), and nothing after Primus is a zone change. Accepted trade-off: quitting after killing Primus but before
  touching the light sends nothing for that run (the run isn't won either).
- **Starless Path win = full clear** (decided 2026-09-27). A Starless Path ending (`ResultType.StarlessPath`) counts as
  clearing **all five worlds** for the Traveler played, at the run's difficulty (cumulative, like any clear). It's
  needed because the Starless Path is always entered partway through a world, before its boss (the Guiding Compass
  quest can't start in a boss room, and side-path rifts only spawn in combat rooms), and the run ends there with no
  zone change. So the zone-change rule alone would never clear the world it was entered from, or any later world.
  *Built:* on `GameResultManager.ClientEvent_OnGameConcluded` with `StarlessPath`, record and send the clears for
  worlds 1–5. Logic is unchanged: a Starless Path win needs the same Traveler copies as the clears it sends.
- **Difficulties:** Deep Sleep, Ominous Dream, Nightmare. Nap has no locations.
- **Cumulative:** a clear on a higher difficulty also sends the same world's checks for every lower difficulty
  (Nightmare sends Nightmare, Ominous Dream and Deep Sleep).
- **Loops:** worlds 1–4 of later loops send nothing. Limbo (`diffLimbo`) is its own difficulty id, which isn't one of the four
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

In vanilla each achievement unlocks exactly one thing, which gives 93 unlocks. All 93 are items, and so are Lacerta and
Mist (vanilla's starting Travelers), grouped as follows.

| Class | Item | Copies | Effect |
|---|---|---|---|
| progression | `Progressive <Traveler>` | 4 per Traveler (36), 2 of them starting items (34 in the pool) | 1st copy unlocks the Traveler; copies 2–4 unlock their 3 alternate memories in the fixed order below |
| useful | `Memory: <name>` (general, non-Traveler Memories locked behind achievements) | 15 | Makes the Memory able to drop in runs |
| useful | `Essence: <name>` | 29 | Makes the Essence able to drop in runs |
| useful | `Lucid Dream: <name>` | 15 | Unlocks the Lucid Dream modifier. A **forced** dream's item is progression and releases it (see "Forced Lucid Dreams") |
| filler | `Mastery: <Traveler>` (one item per Traveler) | 8 per Traveler (72) | +5 mastery levels to that Traveler |
| filler | `Stardust` | the rest (63) | +750 Stardust |

- **Starting Travelers** (replaces vanilla's Lacerta and Mist): each seed picks 2 random Travelers in `generate_early`
  (`self.random.sample` of the sorted Traveler keys). One copy of each one's progressive item is precollected
  (`push_precollected`): it appears in the spoiler's "Starting Items", and the server sends it to the mod as a received
  item from location −2. There's no option and no slot_data field.
- **No traps.**
- **Déjà vu is untouched.** It's the Pure White Dream carry-over system that costs Stardust.
- **Counts:** 34 progression + 59 useful + 72 mastery + 63 Stardust = 228, equal to the location count.
- **Filler targets:** filler alone gives exactly **40 mastery on every Traveler** and **47,250 Stardust**
  (decided 2026-10-03, issue #5). 40 is the level of each Traveler's last story episode (`TravelerStory_<T>_Main8`,
  read from the asset bundles), which unlocks the Starless Path ending (`unlocksPolarisEnding`); stars and star slots
  need at most 35. Levels come in chunks of 5, the granularity of every mastery reward. The Stardust covers the 45,275
  needed for every star level, plus a buffer for star slot unlocks (40 + 20 × n Stardust per extra slot, where n counts
  from 0 past the Traveler's default slot count for that star type). Checked in-game: the slots cost 4,740, so filler
  falls 2,765 short of buying every slot too; accepted (see "Settled questions").
- Vanilla run rewards (Stardust, mastery points) are not counted toward these targets and stay as they are, except
  mastery when `passive_mastery` is off (see "Passive mastery").

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
| Lacerta | Traveler | Incendiary Rounds (Q) | Precision Shot (R) | Double Tap (Identity) |
| Mist | Traveler | Flèche (Q) | Parry (R) | Astrid's Masterpiece - Priorité (Identity) |

The order lives in one data table in the extractor. It is not derived from anything.

## Logic (access rules)

"Copies" means how many of that Traveler's progressive item you have received.

The rules are the same for every Traveler. A starting Traveler's precollected copy counts.

| Location | Needs |
|---|---|
| Achievement that requires a Traveler (e.g. "as Aurena") | 1 copy |
| Any other achievement | nothing |
| "Achievement: The Road Not Taken", with `passive_mastery` off | nothing, but **excluded** (see "Passive mastery") |
| World clear, Deep Sleep | 1 copy |
| World clear, Ominous Dream | 2 copies (Traveler + 1 memory) |
| World clear, Nightmare | 3 copies (Traveler + 2 memories) |
| Goal | `goal_traveler_count` Travelers unlocked (the starting ones count). **No memory requirement.** |

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
| `mastery_packs_per_traveler` | range 0–15 | 8 | 0 removes mastery from the pool. 15 × 9 = 135 fills every filler slot. |
| `mastery_pack_value` | range 1–40 | 5 | levels per pack |
| `stardust_pack_value` | range 1–10,000 | 750 | Stardust fills every remaining filler slot, so it can't be 0 |
| `passive_mastery` | toggle | on | See "Passive mastery" |
| `death_link` | toggle | off | |
| `forced_evil_lucid_dreams` | set of Evil Lucid Dream names | empty | See "Forced Lucid Dreams" |
| `forced_chaotic_lucid_dreams` | set of Chaotic Lucid Dream names | empty | See "Forced Lucid Dreams" |
| `shuffle_star_requirements` | toggle | on | See "Shuffled star requirements" |

Remove `boss_locations` and `travelers_required_for_goal`. `slot_data` must carry everything the mod needs: goal
settings, pack values, `passive_mastery`, death_link, the forced Lucid Dreams (`forced_lucid_dreams`: both sets' keys), the shuffled star
requirements (`star_requirements`), the data version and the data hash.

## Forced Lucid Dreams

An optional challenge modeled on the Reverse Heat option of the Hades randomizer (Polycosmos), decided 2026-09-28.
The player lists Lucid Dreams that start **forced on**. Each one stays on in every run until its `Lucid Dream: <name>`
item is received, which **releases** it: from then on it's a normal unlocked dream the player can turn on or off.

- **Two options, both empty (off) by default:** `forced_evil_lucid_dreams` and `forced_chaotic_lucid_dreams`. Each only
  accepts dreams of its own type. Good dreams can't be forced: they make runs easier.
- **The types** (`LucidDream.type`, stored in the prefabs, read from the asset bundle 2026-09-28; the extractor's
  `LUCID_DREAM_TYPE` table):
  - Evil (6): Fish Scales, Grievous Wounds, Mad Life, Marsh of Destiny, Overpopulation, Prudent Jellyfish.
  - Chaotic (5): Embrace Mortality, Harmless Whispers, Sparkling Dream Flask, The Darkest Urge, WILD.
  - Good (4): Bland Star Soup, Bon Voyage, False Lifeline, Kind Armadillo.
- **No new items.** A forced dream's existing item is its release. Pool, IDs and `data_hash` are unchanged.
- **Forced dreams' items are progression** (Evil and Chaotic alike), so fill puts them in the priority world clears:
  removing these modifiers is key to winning. No access rule needs them; a seed may ask for a win with every forced dream
  still on. That's the challenge the player opted into.
- **No "minimal" variant** (dreams forced permanently, with no release item). It has no multiworld part: players can
  already turn dreams on themselves.
- **Stored in the profile.** On every login the mod copies `forced_lucid_dreams` into the profile's AP records
  (`AP:forced:<LucidDream_*>` in `experienceFlags`), so forcing also works offline. "Still forced" = in that list and
  not yet in the unlock record. Forced dreams are **not** unlocked in the profile before their item arrives: Limbo opens
  when 4 Evil dreams are unlocked (`GameMod_Limbo.IsLimboUnlocked`), and that would open it early.
- **Lobby enforcement** (the host only; `activeLucidDreams` is a server setting). The technique Limbo uses to keep only
  Evil dreams on (`GameMod_Limbo.EnforceGameRules`), turned around, in either lobby mode: the mod's `Update` adds every
  still-forced dream to `GameSettingsManager.activeLucidDreams` (`AddLucidDream`) whenever it's missing, and a
  `PlayLobbyManager.AddStartGameCondition` refuses to start with "Forced by Archipelago: ..." if one is still missing.
  Skipped in Limbo lobbies (Limbo removes non-Evil dreams itself, and Limbo sends no checks) and when continuing a saved
  run (its dreams come from the save).
- **A run only counts if every still-forced dream is active.** When a run is ready (`GameManager.CallOnReady`), the mod
  compares the still-forced dreams with the dreams in effect (the `LucidDream` actors, plus the synced
  `activeLucidDreams`). If one is missing, that run's world clears and wins are not recorded or sent, and the player is
  told. Achievements always count. The same rule applies to the host (where it always passes) and to a player who joined
  someone else's lobby (who can't force anything). A continued run is checked the same way.
- **Timing:** a dream released mid-run can be turned off from the next lobby (Lucid Dreams are chosen in the lobby).

## Shuffled star requirements

An optional shuffle of the mastery level each constellation star needs before it can be bought (decided 2026-09-28).
No new numbers: stars trade their vanilla requirements with each other.

- **Option:** `shuffle_star_requirements`, a toggle, on by default.
- **Groups: mastery type × category.** A star only trades levels with stars of the same Traveler (or the common stars,
  which need total mastery) and the same category (Destruction, Life, Imagination, Flexible). That's 3 common groups and
  4 per Traveler (39 groups, 305 stars). Each group keeps its vanilla set of levels, so Flexible stars stay on their
  5/15/25/35 steps, and the highest requirement stays 35 per Traveler and 75 total: the filler targets (40 mastery per
  Traveler) still cover every star.
- **No logic.** Stars and mastery aren't in logic. Pool, IDs and `data_hash` are unchanged.
- **Generation:** the apworld shuffles each group with the seed's random in `generate_early` and sends every star's level
  as `star_requirements` in slot_data (`{Se_Star_*: level}`; empty when off). No spoiler section: the constellation
  screen shows every requirement. The extractor writes the star list (`stars`: key, Traveler, category, vanilla level,
  from `RawData/en-US/stars.json`) to `game_data.json`, outside `data_hash`, so older seeds keep working and get vanilla
  requirements.
- **How the game uses it** *(resolved from code)*: the requirement is the `StarEffect.requiredLevel` field of the star
  prefab. Only the lobby's constellation screens read it: the buy check (`UI_Lobby_Constellations_StarDetails`), the
  locked look (`UI_Lobby_Constellations_StarItem`), the number on the icon (`UI_StarIcon`) and the list order
  (`UI_Lobby_Constellations_StarList`). Nothing checks it at run start or on profile load, so a star already bought stays
  usable whatever its requirement becomes.
- **Stored in the profile.** On every login the mod copies `star_requirements` into the profile's AP records
  (`AP:star:<Se_Star_*>=<level>` in `experienceFlags`), so the levels also apply offline. Keys the game doesn't have are
  logged and skipped; a star missing from the list (e.g. new in a game update) keeps its vanilla level.
- **Applied in memory, locally.** The game unloads and reloads prefabs (`DewResources.UnloadUnused` on the title screen,
  lobby entry and zone loads), so the mod sets `requiredLevel` on every star `DewResources` hands out: postfixes on
  `DewResources.Load` (fresh loads and guid lookups, e.g. `AssetRef`) and the non-generic `DewResources.GetByType` (its
  cache skips `Load`). It remembers each star instance with its vanilla level, and on mod load, profile change and login
  updates every instance it has seen. Unmarked profiles get vanilla levels; unloading the mod puts them back. The game's
  JSON overrides could also change `requiredLevel`, but they are fixed files, not per seed, and the host sends them to
  joining players; the mod's change never leaves the local game, so a non-AP co-op player is unaffected.

**Data compatibility.** `game_data.json` carries `data_format_version` (the JSON layout) and `data_hash`: a SHA-256 of
every field the mod or the generator acts on (IDs, keys, kinds, unlocks, logic data, the difficulty ids and ranks),
leaving out display names (`apworld/shape_of_dreams/data/data_hash.py`). The mod refuses a seed unless both equal its own embedded
copy, so an old mod can't silently play a seed generated after a game update added items or locations. IDs are also
kept in `id_history` (every key→ID ever assigned, including retired keys), so an ID is never reused for other content
and a key that disappears and comes back keeps its ID.

## Passive mastery

An optional challenge (decided 2026-10-03, issue #5): with `passive_mastery` off, runs earn no Traveler mastery, so
mastery only comes from `Mastery: <Traveler>` items and the player's strength depends on the multiworld.

- **Option:** `passive_mastery`, a toggle, on (vanilla) by default. slot_data `passive_mastery`; a missing key (older
  seeds) means on.
- **Generation:** when it's off, "Achievement: The Road Not Taken" (`ACH_THE_ROAD_NOT_TAKEN`, a Starless Path win) is
  `EXCLUDED`: the Starless Path needs a Traveler at mastery 40, which then only Mastery items give. It gets no access
  rule, and Mastery items stay filler. With too few Mastery items to reach 40 the check can never be done; that's fine,
  it only holds filler. AP keeps a world-set exclusion over the player's `priority_locations`. Pool, IDs and
  `data_hash` are unchanged.
- **Why the reward itself is 0** *(resolved from code)*: `DewSave.ConsumeGameResult` turns a run into points with
  `Dew.GetRewardedMasteryPoints`, adds them, and reports them as `LastGamePlayReward.heroMasteryPoints`. The results
  screen (`UI_PlayRewardAnnouncer`) animates from "current points − heroMasteryPoints", so taking points back afterwards
  would show fake level-ups. With a 0 reward it skips the mastery panel, as vanilla does for a run that earned nothing,
  and a conceded run records 0 points to take back if it's finished later.
- **Mod:** on login the mod writes `AP:nopassivemastery` to `experienceFlags` when the option is off (and removes it
  when on), so offline runs follow it too. A prefix/finalizer on `ConsumeGameResult` sets a flag while it runs on a
  profile with that record, and a postfix on `GetRewardedMasteryPoints` returns 0 while the flag is set. That covers
  every way a run is rewarded: the end of a game (`GameResultManager`) and a run left unrewarded
  (`lastUnrewardedGameResult`), caught up on the title screen (`TitleManager.CheckForOtherRoutine`) or in the lobby.
  Redeem codes and the free-version mastery reward also call `GetRewardedMasteryPoints`, outside `ConsumeGameResult`,
  and are untouched.
- **Known limitation:** mods load about 2 seconds after the title scene (`DewMod.OnInit`'s cancel countdown), and the
  title screen can catch up a run left unrewarded from the previous session before that, so such a run still earns
  mastery once. Accepted.
- **Co-op:** each player's client rewards their own profile, so it only affects that player.

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
  (`AP:unlock:<target>`), world clears (`AP:clear:<location key>`), wins (`AP:win:<Traveler>:<difficulty id>`), the
  Stardust counter (`AP:applied:STARDUST=<n>`), the forced Lucid Dreams (`AP:forced:<key>`) and the shuffled star
  requirements (`AP:star:<key>=<level>`). The mastery counters (`AP:applied:MASTERY_<Traveler>=<n>`) live in
  `DewProfileStats.recoveredLossPoints`, in the `stats` file, so each is written in the same save as the value it
  protects.
- **Known limitation: the game's stats recovery** (accepted 2026-09-27). At startup, before mods load, the game compares
  the `stats` file with its daily backups. If total mastery or play time went down, it adds the lost mastery levels back
  (`DewProfileStats.GetRecoveryDelta`) but not our mastery counters, so Mastery items applied between that backup and
  the loss get applied a second time. There's no exact repair: that needs the backups' counters. The over-grant is
  bounded filler, so the mod only reports it: each new `loss_…` entry the game adds to `recoveredLossPoints` after
  binding logs a warning and shows a notice once (`AP:seenloss:<entry>`; entries from before the binding are marked seen
  at bind time).
- **The unlock record.** `DewProfile.Validate` runs on every profile load and re-derives unlocks from achievements: it
  locks the target of every incomplete achievement and unlocks the target of every completed one. `UnlockHero` also
  unlocks a Traveler's alternate memories whose achievement is complete. On a marked profile the mod therefore keeps
  its own record of what AP has granted, and after `Validate`, on mod load, on connect and after items it sets every
  managed unlock (every Traveler, the 93 targets, plus a locked Traveler's own memories) to match, using the game's `Unlock*`/`Lock*`
  functions. This works offline too.
- **Travelers come only from items.** A Traveler is unlocked if and only if the unlock record has it; the starting
  Travelers' precollected copies arrive as received items (location −2, shown as "(starting item)"). `Validate` unlocks
  Lacerta and Mist and locks the rest on every load; the unlock record undoes that.
  - **Profiles bound before random starting Travelers** have no record for Lacerta and Mist. Binding now also writes
    `AP:format:2`; a marked profile without it keeps Lacerta and Mist unlocked (offline play; the mod refuses those
    seeds online).
  - **The binding save already has the starting Travelers.** On bind, the received items (sent in the same server
    reply as `Connected`) are applied before the bound profile's first save. Binding waits for that first item sync;
    if it doesn't come within 5 s, the mod disconnects and binds nothing.
- **Preferred Traveler.** The lobby spawns `PreferredGameSettings.hero` through `DewPlayer.CmdSetHeroType` without a lock
  check (neither does the server), and a missing lobby-type entry defaults to Lacerta. On a marked profile, a prefix on
  `CmdSetHeroType` replaces a locked Traveler with the first unlocked one in `Dew.HeroOrder` and stores it back; after
  every unlock sync the profile's existing preferred-settings entries are repaired the same way.

### Save profile binding
Every AP seed/slot gets its own game profile. Your normal save must never be read or written by AP logic.

1. The player creates a new profile in the game's own UI.
2. They connect from the title screen and confirm "bind this profile to <seed>/<slot>" (`ap_bind`). Binding only works
   on the title screen, never in a lobby or a run (`Title` scene, no current online lobby, Mirror client/server
   inactive, no `GameManager`):
   the lobby's Travelers and a run's loot pool were built from the unbound unlocks.
3. The mod refuses to bind a profile that isn't fresh: it must have no completed achievements and no runs played. There
   is also a deliberate override for recovery (`ap_bind_force`). *Built test:* no completed achievements, no recorded
   game results (`lastGameResults`, `recentlyConcededGames`) and a total Traveler play count of 0. In-game 2026-09-27: a
   never-played profile with `didPlayTutorial=True` passes (0 runs, play count 0). Finishing the tutorial run wasn't
   tested; if it makes a profile fail, `ap_bind_force` covers it.
   **Bind after login.** `ap_bind` doesn't write the marker. It logs in provisionally; nothing acts on the profile,
   because every action needs Bound. Only after `LoginSuccessful` and the slot_data version/hash check does the mod write
   the marker, after re-checking that the same profile is loaded (attempt and profile generation unchanged), that it is
   still fresh (unless forced) and that it is still on the title screen. A wrong slot name or password never marks a
   profile.
4. On bind, write a marker into `DewProfile.experienceFlags`, e.g. `Archipelago:<seed>:<slot>`. It's a free-form
  string list that the game only reads with `Contains`. The marker travels with the profile (Steam Cloud, moves)
  and can't get separated from it.
5. **Seed check before login.** `ConnectAsync` returns `RoomState.Seed`. If the loaded profile carries a marker and it
   doesn't match, refuse and don't log in. An unmarked profile logs in only through `ap_bind` (step 3).
6. **A single guard before every action** (send, apply, suppress, gate): the loaded profile carries the marker for
   the current session's seed/slot, `DewSave.profileMainPath` is not null (the Transient profile has a null path), and
   the last profile load didn't fail. Hook `DewSave.LoadProfile` (with its result), `CreateProfile` and `ConvertProfile`
   to re-check. On a switch or a failed load, disconnect, and drop queued work that was tagged with the old profile. A
   failed `LoadProfile` can leave a mix of two profiles loaded (e.g. the new main file with the old stats), so AP stays
   off until the next successful load. The Steam block (step 8) checks the raw marker instead, because it runs inside
   `LoadProfile`, before that flag is updated.
   A connection that is still being made is tracked from the start, so a disconnect (new `ap_connect`, profile switch,
   unloading the mod) closes its socket too.
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
No special handling, except for Forced Lucid Dreams (see there). Each player's own profile, mod and slot are independent. The shared loot pool is the union of
the players' unlocks, which is vanilla co-op behavior. Achievement and world-clear checks work on a joining client
(code review): achievements are tracked per client, the zone-loaded event is an RPC to every client, and the room load
before it syncs the zone index.

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

## Settled questions
- ~~Map the game's difficulty IDs~~ The asset catalog has `diffTutorial`, `diffEasy`, `diffNormal`, `diffHard`,
  `diffNightmare` and `diffLimbo`. The build maps Nap = `diffEasy`, Deep Sleep = `diffNormal` (the default),
  Ominous Dream = `diffHard`, Nightmare = `diffNightmare`. Verified in-game 2026-09-27 (`[AP] Difficulty ids -> display
  names`: Tutorial, Nap, Deep Sleep, Ominous Dream, Nightmare, Limbo). The build has 4 worlds per loop
  (`zoneCountByTier=[1,1,1,1]`).
- ~~World-clear detection~~ Zone transition, world = `currentZoneIndex` on arrival (see "Locations").
- ~~Star slot counts per Traveler (the Stardust buffer)~~ Logged in-game 2026-09-27: 80 buyable slots cost 4,740
  Stardust, more than the buffer (1,975 since Stardust items give +750), so filler alone falls 2,765 short of buying
  everything. **Accepted as is**: vanilla run and achievement Stardust covers the rest. ~~Mastery cap~~,
  ~~fresh-profile test~~ and ~~UnknownFate~~: see above.
- ~~`isAlteringGameplay`~~ Kept on.
- ~~DeathLink for a joining co-op player~~ Accepted as a known limitation (see "DeathLink").
