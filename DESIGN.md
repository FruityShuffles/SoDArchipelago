# SoDArchipelago design

The agreed randomizer design for Shape of Dreams. Where the code and this document disagree, this document wins.
Item/location IDs are frozen once a version is released: the extractor keeps every key's ID through `id_history`.

## Locations (302 by default)

| Kind | Count | Progress type | Notes |
|---|---|---|---|
| Achievements (`ACH_*`) | 93 | default | Every achievement in `RawData/en-US/achievements.json`. "Who's the Prey Now" is **excluded** (`BROKEN_ACHIEVEMENTS`, issue #6). |
| World clears | 135 | **priority** (Deep Sleep), default | 5 worlds × 3 difficulties × 9 Travelers. The 45 Deep Sleep clears are the only priority locations. |
| Souvenirs | 17 | **excluded** | Every souvenir the lizard shop sells (see "Souvenirs"). Always on, no option. |
| Jonas's Wares | 0–100 (30 by default) | default | One random unbought ware per Jonas visit, bought with gold; host/solo only |
| Shrines | 9 | default | First successful use by the local player; always on |
| Quests | 6 | default | First completion; always on |
| Artifacts | 12 | **excluded** | First hand-in to the Dream Teller, recorded by the journal; always on |

- **World structure.** A loop has **4 normal worlds**. The Dream rift in world 4's boss room leads to `Zone_Primus`,
  **world 5**. Nothing after arriving there is a zone change: the Primus boss is a room change and its ending a
  teleport inside that room.
- **World clear, worlds 1–4** = moving on to the next world (`ZoneManager.ClientEvent_OnZoneLoaded`, traveling, not
  loading a save). On arrival `currentZoneIndex` equals the number of the world just left (it starts at -1, is 0 in
  world 1 and keeps counting through loops). Leaving world 4 is always a zone change: into `Zone_Primus` or into the
  next loop. Only indexes 1–4 count (index 5 is leaving loop 2's first world, not a World 5 clear).
- **World clear, world 5** = beating Primus, detected as a **Pure White Dream win**. On
  `GameResultManager.ClientEvent_OnGameConcluded` with `result == ResultType.PureWhiteDream`, record and send the World 5
  clears. That hook also fires on death and concede, so check the result type. Quitting after killing Primus but
  before touching the light sends nothing (the run isn't won either).
- **Starless Path win = full clear.** A Starless Path ending (`ResultType.StarlessPath`) counts as
  clearing **all five worlds** for the Traveler played, at the run's difficulty (cumulative, like any clear): the run
  enters it partway through a world, before that world's boss, and ends there with no zone change. Logic: a
  Starless Path win needs the same Traveler copies as the clears it sends.
- **Difficulties:** Deep Sleep, Ominous Dream, Nightmare. Nap has no locations. Game ids: Nap = `diffEasy`,
  Deep Sleep = `diffNormal` (the default), Ominous Dream = `diffHard`, Nightmare = `diffNightmare`.
- **Priority: Deep Sleep clears only.** Priority fill puts any player's progression there; all 135 clears pulled
  about half of a multiworld's progression into SoD runs. 45 still hold SoD's own progression (34, or 45 with every
  dream forced), and any win collects them.
- **Cumulative:** a clear on a higher difficulty also sends the same world's checks for every lower difficulty
  (Nightmare sends Nightmare, Ominous Dream and Deep Sleep).
- **Loops:** worlds 1–4 of later loops send nothing. Limbo (`diffLimbo`) is its own difficulty id, which isn't one of the four
  mapped difficulties, so it sends no world clears and its wins don't count for the goal.
- **Naming:** `Achievement: <display name>`, `World <n> Clear (<Difficulty>): <Traveler>`, for example
  `World 3 Clear (Nightmare): Mist`, and `Souvenir: <display name>`, for example `Souvenir: Mini Aurena`.
- **Location groups** (for players' own `exclude_locations` / `priority_locations`): `Achievements`,
  `World Clears`, one group per Traveler (their achievements and world clears), one per difficulty,
  `Build-Dependent Achievements` (those that need specific Memories) and `Souvenirs` (not in the Traveler groups,
  not even the plushies).

### Souvenirs

Issue #4. Each souvenir the lizard shop can sell is an **excluded** location, and you keep the souvenir as in vanilla.

- **The 17** (the extractor's `SOUVENIRS`): every `Acc_*` that isn't `generatedFromServer` or `excludeFromPool` and
  passes `Dew.IsAccessoryIncludedInGame`, the shop's own filter. Server-generated souvenirs stay out (Modding ToS),
  as do the `Acc_HeroAcc_*` Traveler story rewards.
- **Excluded** because each visit offers 3 random unowned souvenirs: collecting all 17 is luck, Stardust and time. AP
  keeps a world-set exclusion over the player's `priority_locations`. No logic: any run can reach a shop.
- **Owning one is its check**, however it was unlocked: a purchase, or a redeem code (which also takes it out of the
  shop for good). See "Checks".

### Jonas's Wares

Issue #8. `Jonas's Ware 1` through `Jonas's Ware <jonas_wares>` are normal locations that can hold any player's
progression. The catalog reserves 100 stable keys (`WARE_JONAS_<n>`); disabled numbers are absent from the seed.
The `Jonas's Wares` location group contains all possible wares. There is no access requirement: any run can reach Jonas.

- Each visit offers one random enabled ware not yet recorded in `AP:check:` or checked on the connected server
  (including released checks). Offline availability uses the local record. Skipping it costs nothing; a later visit
  or paid shop refresh can offer a different one. Once all are bought, Jonas has only his vanilla stock.
- Only Jonas (`PropEnt_Merchant_Jonas`) adds wares, and only to the solo/host player's own guid-keyed stock. Guests'
  stock stays vanilla. His `OnRefresh` calls `PopulatePlayerMerchandises`, so the population hook handles both.
- The ware is a vanilla Treasure entry with `itemName = Treasure_CloakOfGuidance`, `count = 1` and
  `customData = AP:<location key>`, so it gets vanilla purchase checks and pricing. No Treasure is spawned: intercept
  `SpawnMerchandise` after the server has spent gold, record/save/send the check, and suppress the placeholder's effect.
- The local host's shop shows the AP icon; the tooltip shows the scouted item, recipient and location. Purchased
  stock is disabled. A stale ware restored by a continue save is refused before spending gold. Tooltip names are
  player-supplied: encode them so TMP shows them literally.
- Scout every enabled ware once per connection with `LocationScouts`, `HintCreationPolicy.None` (no hints).
  Cache item/recipient strings in the marked profile (`AP:scout:<key>:<base64 item>:<base64 owner>`); the
  connection/profile-tagged main-thread queue writes them. Persist `AP:wares=<count>` on login for offline stock.
  Missing scout data or an offline shop displays "an unknown ware". Offline purchases still record their checks.
- Buying and quitting before the next continue save may restore the gold while keeping the check, as in vanilla.

### Pilgrimage checks

Issue #9. These 27 checks are always on, independently of `in_run_items`, with no access rules. They count in every
run, including Nap, Limbo, runs blocked by Forced Lucid Dreams, and co-op as host or joining player. Vanilla effects
and rewards are kept. Their groups are `Shrines`, `Quests` and `Artifacts`.

- **Shrines (9):** `Shrine: <name>` for Pot of Greed, Maw of Doom, Hatred, Paradox, Mirror of Remorse, Destiny,
  Entanglement, Altar of Cleansing and Ascension. Patch the client receiver of `RpcInvokeOnSuccessfulUse` and record
  `AP:check:<Shrine type>` only when the supplied entity's owner is `DewPlayer.local`. Failed interactions do not
  reach that RPC. Other players' uses do not count for the local player.
- **Quests (6):** `Quest: <name>` for Stray Memory, Star Seeker's Journal, Fragment of Radiance, Call of the Ravenous,
  Consort of Night and Hunted by Obliviax (escaping her). When a quest is removed with `state == Completed`, record
  `AP:check:<Quest type>`. Also check after `DewQuest.DeserializeSyncVars`: the base Actor's inactive hook can invoke
  removal before the derived quest state is read from the same network packet. Both paths share the idempotent record;
  failed and ongoing quests send nothing.
- **Artifacts (12):** `Artifact: <name>` for every artifact prefab not `excludeFromPool`. Take names from the game's
  localization: several differ from the prefab keys. A before/after patch
  on the loaded marked profile's `DiscoverArtifact` saves/sends its check when the native journal status becomes
  `Complete`. Merely picking one up does not count. Hand-in reaches every player's client. No `AP:check:` record:
  the native flag persists and the resend reads it. Artifacts stay excluded even over `priority_locations` because
  the player cannot choose which one appears.
- Leave out checks already covered by achievements: Disintegration, Guidance/Blessed Guidance and the treasure-map
  quests. Also omit common/reward or world-specific shrines, Secret Meeting/Guiding Compass (mastery 40), Lost Soul
  (co-op knockdowns), curse, Limbo and tutorial quests, and excluded artifact prefabs.

## Items

In vanilla each achievement unlocks exactly one thing, which gives 93 unlocks. All 93 are items, and so are Lacerta and
Mist (vanilla's starting Travelers), grouped as follows.

| Class | Item | Copies | Effect |
|---|---|---|---|
| progression | `Progressive <Traveler>` | 4 per Traveler (36), 2 of them starting items (34 in the pool) | 1st copy unlocks the Traveler; copies 2–4 unlock their 3 alternate memories in the fixed order below |
| useful | `Memory: <name>` (general, non-Traveler Memories locked behind achievements) | 15 | Makes the Memory able to drop in runs |
| useful | `Essence: <name>` | 29 | Makes the Essence able to drop in runs |
| useful | `Lucid Dream: <name>` | 15 | Unlocks the Lucid Dream modifier. A **forced** dream's item is progression and releases it (see "Forced Lucid Dreams") |
| useful | `Mastery: <Traveler>` (one item per Traveler) | 8 per Traveler (72) | +5 mastery levels to that Traveler; always useful, independently of passive mastery and pack settings |
| filler | `Stardust` | remaining baseline slots (80) + unallocated new slots | Exact share of `stardust_total` (+675 with 80 packs and default total) |

- **Starting Travelers** (replaces vanilla's Lacerta and Mist): each seed picks 2 random Travelers in `generate_early`
  (`self.random.sample` of the sorted Traveler keys). One copy of each one's progressive item is precollected
  (`push_precollected`): it appears in the spoiler's "Starting Items", and the server sends it to the mod as a received
  item from location −2. There's no option and no slot_data field.
- **Déjà vu is untouched.**
- **Baseline counts:** 34 progression + 131 useful (including 72 mastery) + 80 Stardust filler = 245, equal to the baseline location count.
  With default wares and pilgrimage checks, the pool has 34 progression + 131 useful + 137 filler = 302.
- **Mastery classification:** every Mastery item is useful under all options. It cannot be placed
  on excluded checks; mastery remains outside progression logic. Stardust remains filler.
  A solo seed needs at least `93 + 9 × mastery_packs_per_traveler` non-excluded checks (165 by default).
- **Pool reward targets:** the default items alone give exactly **40 mastery on every Traveler** (issue #5) and
  **54,000 Stardust** (issue #4). 40 is the level of each Traveler's last story episode (`TravelerStory_<T>_Main8`,
  read from the asset bundles), which unlocks the Starless Path ending (`unlocksPolarisEnding`); stars and star slots
  need at most 35. The Stardust covers everything it can buy: every star level, souvenir and star slot (53,415).
- Vanilla run rewards (Stardust, mastery points) are not counted toward these targets and stay as they are, except
  mastery when `passive_mastery` is off (see "Passive mastery").

### Alternate memory order

Each Traveler's three locked memories are one per slot (Bismuth's are all QR). Progressive copies unlock them in this
order: **Q → R → Identity**. The order is a hand table in the extractor, not derived from anything.

## Logic (access rules)

"Copies" means how many of that Traveler's progressive item you have received.

The rules are the same for every Traveler. A starting Traveler's precollected copy counts.

| Location | Needs |
|---|---|
| Achievement that requires a Traveler (e.g. "as Aurena") | 1 copy |
| "Achievement: Vivid Dream" (enter the Pure White Dream on Nightmare: a World 4 Nightmare clear) | 3 copies of any Traveler |
| Any other achievement | nothing |
| Jonas's Ware | nothing; purchased from Jonas while solo/host |
| Shrine, Quest, Artifact | nothing; artifacts are always excluded |
| "Achievement: The Road Not Taken" (a Starless Path win: a Traveler at mastery 40) | all 36 copies (every Traveler fully unlocked); **excluded** with `passive_mastery` off (see "Passive mastery") |
| World clear, Deep Sleep | 1 copy |
| World clear, Ominous Dream | 2 copies (Traveler + 1 memory) |
| World clear, Nightmare | 3 copies (Traveler + 2 memories) |
| Goal | `goal_traveler_count` Travelers unlocked (the starting ones count). **No memory requirement.** |

- Nothing requires a Traveler's third memory. It stays progression, but the logic never waits on it.
- **Achievement → Traveler mapping:** the extractor's hand table (27 achievements, 3 per Traveler), not the reward.

## Goal

Win a run at `goal_difficulty` **or harder** with `goal_traveler_count` **different** Travelers.

- "Win" means reaching either ending: `ResultType.PureWhiteDream` or `ResultType.StarlessPath`.
- `UnknownFate` doesn't count: only demo/booth builds produce it.
- Wins are recorded in the bound profile (Traveler + difficulty id), so a win played offline counts on the next connect.
- The mod records in the bound profile which Travelers have counted. It sends `ClientGoal` when the count is reached.

## Options (YAML)

| Option | Type | Default | Notes |
|---|---|---|---|
| `goal_difficulty` | choice: `deep_sleep`, `ominous_dream`, `nightmare` | `nightmare` | |
| `goal_traveler_count` | range 1–9 | 9 | |
| `mastery_packs_per_traveler` | range 0–15 | 8 | 0 removes mastery from the pool |
| `mastery_pack_value` | range 1–40 | 5 | levels per pack |
| `stardust_total` | range 0–1,000,000 | 54,000 | Total Stardust divided exactly over the seed's Stardust slots |
| `in_run_items` | toggle | off | New slots may hold Map Blessings and Treasures |
| `traps` | toggle | off | 20% of new slots become Curse traps |
| `jonas_wares` | range 0–100 | 30 | Number of Jonas's Wares (#8), host/solo only |
| `passive_mastery` | toggle | on | See "Passive mastery" |
| `death_link` | toggle | off | |
| `forced_evil_lucid_dreams` | set of Evil Lucid Dream names | empty | See "Forced Lucid Dreams" |
| `forced_chaotic_lucid_dreams` | set of Chaotic Lucid Dream names | empty | See "Forced Lucid Dreams" |
| `shuffle_star_requirements` | toggle | on | See "Shuffled star requirements" |

`slot_data` must carry everything the mod needs: goal settings, mastery pack values, `stardust_total`,
`stardust_item_count`, `in_run_items`, `traps`, `jonas_wares`, `passive_mastery`, death_link, the forced Lucid Dreams
(`forced_lucid_dreams`: both sets' keys), the shuffled star requirements (`star_requirements`), the data version and
the data hash.

[Universal Tracker](https://github.com/FarisTheAncient/Archipelago/releases) rebuilds the world from
slot_data alone (`ut_can_gen_without_yaml`; `interpret_slot_data` → `generate_early`), so slot_data must also carry
every setting that changes logic or location types. Starting Travelers aren't needed: UT drops precollected items and
uses the starting items the server sends.

## Forced Lucid Dreams

An optional challenge. The player lists Lucid Dreams that start **forced on**. Each one stays on in every run until
its `Lucid Dream: <name>` item is received, which **releases** it: from then on it's a normal unlocked dream the
player can turn on or off.

- **Two options, both empty (off) by default:** `forced_evil_lucid_dreams` and `forced_chaotic_lucid_dreams`. Each only
  accepts dreams of its own type. Good dreams can't be forced: they make runs easier.
- **Types** come from the prefabs (`LucidDream.type`; the extractor's `LUCID_DREAM_TYPE`): 6 Evil, 5 Chaotic, 4 Good.
- **No new items.** A forced dream's existing item is its release.
- **Forced dreams' items are progression** (Evil and Chaotic alike), so fill puts them in the priority Deep Sleep clears:
  removing these modifiers is key to winning. No access rule needs them; a seed may ask for a win with every forced dream
  still on.
- **Stored in the profile.** On every login the mod copies `forced_lucid_dreams` into the profile's AP records
  (`AP:forced:<LucidDream_*>` in `experienceFlags`), so forcing also works offline. "Still forced" = in that list and
  not yet in the unlock record. Forced dreams are **not** unlocked in the profile before their item arrives: Limbo opens
  when 4 Evil dreams are unlocked (`GameMod_Limbo.IsLimboUnlocked`), and that would open it early.
- **Lobby enforcement** (the host only; `activeLucidDreams` is a server setting), in either lobby mode, like
  `GameMod_Limbo.EnforceGameRules`: the mod's `Update` adds every still-forced dream to
  `GameSettingsManager.activeLucidDreams` (`AddLucidDream`) whenever it's missing, and a
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

An optional shuffle of the mastery level each constellation star needs before it can be bought.
No new numbers: stars trade their vanilla requirements with each other.

- **Option:** `shuffle_star_requirements`, a toggle, on by default.
- **Groups: mastery type × category.** A star only trades levels with stars of the same Traveler (or the common stars,
  which need total mastery) and the same category (Destruction, Life, Imagination, Flexible). Each group keeps its
  vanilla set of levels, so the highest requirement stays 35 per Traveler and 75 total, within the pool reward targets.
- **No logic.** Stars and mastery aren't in logic.
- **Generation:** the apworld shuffles each group with the seed's random in `generate_early` and sends every star's level
  as `star_requirements` in slot_data (`{Se_Star_*: level}`; empty when off). No spoiler section: the constellation
  screen shows every requirement. The extractor writes the star list (`stars`: key, Traveler, category, vanilla level,
  from `RawData/en-US/stars.json`) to `game_data.json`, outside `data_hash`, so older seeds keep working and get vanilla
  requirements.
- **How the game uses it:** the requirement is the star prefab's `StarEffect.requiredLevel`. Only the lobby's
  constellation screens read it, so a star already bought stays usable whatever its requirement becomes.
- **Stored in the profile.** On every login the mod copies `star_requirements` into the profile's AP records
  (`AP:star:<Se_Star_*>=<level>` in `experienceFlags`), so the levels also apply offline. Keys the game doesn't have are
  logged and skipped; a star missing from the list (e.g. new in a game update) keeps its vanilla level.
- **Applied in memory, locally.** The game unloads and reloads prefabs (`DewResources.UnloadUnused` on the title screen,
  lobby entry and zone loads), so the mod sets `requiredLevel` on every star `DewResources` hands out: postfixes on
  `DewResources.Load` (fresh loads and guid lookups, e.g. `AssetRef`) and the non-generic `DewResources.GetByType` (its
  cache skips `Load`). It remembers each star instance with its vanilla level, and on mod load, profile change and login
  updates every instance it has seen. Unmarked profiles get vanilla levels; unloading the mod puts them back. Don't use
  JSON overrides: the host sends them to joining players.

**Data compatibility.** `game_data.json` carries `data_format_version` (the JSON layout) and `data_hash`: a SHA-256 of
every field the mod or the generator acts on (IDs, keys, kinds, unlocks, logic data, the difficulty ids and ranks),
leaving out display names (`apworld/shape_of_dreams/data/data_hash.py`). The mod refuses a seed unless both equal its
own embedded copy. `id_history` keeps every key→ID ever assigned, so an ID is never reused and a returning key keeps it.

## Passive mastery

An optional challenge (issue #5): with `passive_mastery` off, runs earn no Traveler mastery, so
mastery only comes from `Mastery: <Traveler>` items and the player's strength depends on the multiworld.

- **Option:** `passive_mastery`, a toggle, on (vanilla) by default. slot_data `passive_mastery`; a missing key (older
  seeds) means on.
- **Generation:** when it's off, "Achievement: The Road Not Taken" (`ACH_THE_ROAD_NOT_TAKEN`, a Starless Path win) is
  `EXCLUDED`: the Starless Path needs a Traveler at mastery 40, which then only Mastery items give. Its access rule
  (all 36 copies, see "Logic") stays. With too few Mastery items to reach 40 it can never be done; that's fine, it
  only holds filler. AP keeps a world-set exclusion over the player's `priority_locations`.
- **Why the reward itself is 0:** `DewSave.ConsumeGameResult` turns a run into points with
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
- **Known limitation** ([issue #13](https://github.com/FruityShuffles/SoDArchipelago/issues/13), accepted): a run
  in progress is saved as an unrewarded conceded result, and after a mid-run game close the title screen rewards it
  before mods load (`DewMod.OnInit` waits for the title scene, then a 2-second cancel countdown), with the normal
  mastery popup. The mastery is temporary: a conceded reward is recorded in `recentlyConcededGames` and taken back the
  next time that run is rewarded (continued, then quit or finished). It stays only if the run is abandoned. Until then
  the extra levels count in the lobby (star requirements, Traveler story claims).
- **Co-op:** each player's client rewards their own profile, so it only affects that player.

## Client mod behavior

### In-run content foundation

Issue #7 is the shared infrastructure for #8–#12. The original 245 checks are the baseline. #8 adds
`jonas_wares` checks; #9 adds 9 shrines, 6 quests and 12 excluded artifacts. This gives
57 new slots and 302 total checks with defaults. The enabled location list is built before regions and items.

- Only the new slots take part in the in-run mix; the baseline's remaining slots (80 by default) stay Stardust.
- With `traps` on, split new slots 20% curses / 80% remaining by largest-remainder rounding. Split curses by weights
  Mild 3, Potent 2, Intense 1. With `in_run_items` on, split the remaining new slots by the following weights:
  each of 10 uncommon blessings 3; Totally Genuine Treasure Map 4; Treasure Map and Determination Shard 3 each;
  Cloak of Guidance and Clairvoyance 2 each; each of 3 common blessings 2; each of 2 rare blessings 1.
  Otherwise the remaining slots become Stardust. All in-run items are filler; curses are traps.
- All splits use integer largest-remainder allocation, with ties broken by table order, so identical settings give
  identical counts.
- Divide `stardust_total` by the number of Stardust items; the first remainder
  copies received give one extra Stardust each. Thus all seeded Stardust items sum exactly to the configured total,
  including when there are more packs than Stardust or the total is zero. Additional server-granted copies give
  the quotient. Count seeded copies after fill, including starting Stardust and ItemLink deliveries, so common
  options and replacement filler keep the exact total. slot_data carries this resolved count, the total and all
  new options; UT preserves the server's count when rebuilding the world.
- Pending in-run items are received copies minus `AP:applied:<KEY>=n` in the main profile. The main-thread landing
  loop requires Bound, a connected socket, a ready unconcluded run, and a local solo/host player. Item-kind handlers
  (#10–#12) return false if no valid target exists, leaving the counter unchanged. On success, increment and save
  the counter, then show the item and sender. Unregistered kinds wait. Delivery never happens offline, in the lobby,
  during transitions or for joining players. Counters survive restarts; reconnect restores the full received list.
  Per-frame eligibility uses cached `softInstance` manager references; `instance` searches the entire scene twice
  when a manager is missing and must not be polled while waiting in the title screen or lobby.
- Reuse the game's vanilla content, targeting, effects and networking. No custom prefabs or network messages.
  A delivered item lost by quitting before the next continue save remains spent (the counter does not rewind).
- Ware, shrine and quest first-time checks use `AP:check:<location key>` in `experienceFlags`. Record and save at once,
  send when Bound, and resend all records on reconnect. Artifact checks use the game's discovered flag (#9).

### Map Blessings

Issue #10. All 15 `blessing` items use their catalog `target` to add a vanilla room modifier ahead on the world map.
The shared in-run loop delivers them. Vanilla co-op guests share the resulting room.

- Keep each modifier's vanilla color, icon and tooltip.
- Placement calls `ZoneManager.TryGetNodeIndexForNextGoal` with the vanilla treasure-map settings. For a main modifier
  (the three common bonuses and Lizard Shop), use distance 2–4, `preferCloserToExit = true`, `avoidMainModifier = true`,
  as Totally Genuine Treasure Map does. For shrines and Artifact, use distance 3–4 and allow a main modifier, as Treasure
  Map does. Read `isMain` from the vanilla prefab. The helper chooses unvisited, non-sidetrack combat nodes and treats
  distance as a preference. No eligible node means pending, including at a world's end or in Primus.
- Before searching, defer in exit-boss rooms, sidetracks, special-generated maps (including Primus), transitions or
  invalid room/node state. The vanilla helper can return a node behind an exit or use disconnected sidetrack distances;
  a successful search alone does not establish a reachable destination ahead.
- Reject a selected node already carrying the same modifier type; retry on later updates without spending the copy.
  Distinct shrine modifiers may still share a node. Native shrine restoration matches by shrine class, so identical
  shrine copies on one node would attach their cleanup to the same restored shrine. Artifact blessings also wait for
  QuestManager and an empty shared party artifact slot. Do not impose world generation's once-per-loop quota: after a
  hand-in, another AP artifact can be picked up.
- Call `AddModifier` with `isForceRevealed = true`; its vanilla path syncs the node, spawns the modifier (and any
  shrine) on room arrival and draws the marker even on an unexplored node.
- Broadcast the vanilla `WorldNode` ping with the local host as sender, avoiding the command's chat rate limit for
  batches of blessings. This flashes the map button and posts the vanilla chat message. Then save the applied counter
  and show `Blessing from Alice: Mirror of Remorse, marked on your map.` Starting/server grants identify their source.
  A ping exception is logged but never retries a modifier already placed. A missing ping manager leaves the item pending.
- Hunter behavior stays vanilla: a hunted node loses its main bonus while shrines remain. No replacement or refund.
  Each received copy requires its own successful placement.

### Treasures

Issue #11. The five catalog `treasure` items deliver their vanilla effect for free through the shared landing loop.
Call `Dew.InstantiateAndSpawn` as Jonas's `SpawnMerchandise` does, at the local hero's `agentPosition`, setting
`player`, `hero`, `price = 0`, `merchant = null` and `customData = null` before spawn. Their `OnCreate` runs the
vanilla effect and networking; the shared loop records the spawned delivery and announces the item and sender.
Host-side `OnCreate` is dispatched by the queued local Mirror spawn message; the counter does not wait for that
callback. Missing prefabs or an inactive/missing hero leave the item pending.

| Treasure | Lands when | Vanilla effect |
|---|---|---|
| Cloak of Guidance | `!ZoneManager.isHuntAdvanceDisabled` (not Primus) | Adds the prefab's skipped Hunter turns (2 in the installed game), clears about-to-be-taken nodes, chat notice, destroys itself |
| Clairvoyance | Its prefab's `ShouldBeIncludedInPool` passes | Reveals every unvisited node fully and posts a chat notice |
| Determination Shard | Any ready run | Adds its own saved death interrupt to the host's hero; restores 25% health when it saves them |
| Treasure Map | The quest's node helper finds a destination | Starts `Quest_TreasureMap`, a Hidden Stash, distance preference 3–4, allows a main modifier |
| Totally Genuine Treasure Map | The quest's node helper finds a destination | Starts `Quest_SuspiciousTreasureMap`, 60% Hidden Stash / 20% Gold Everywhere / 20% Ambush, distance preference 2–4, avoids main modifiers |

- Clairvoyance's `ShouldBeIncludedInPool` calls the same `HasNonRevealedArea` predicate as `CanBePurchased`, without
  the failure message that requires a buyer on the instance. Never mutate the shared prefab. After its effect applies,
  explicitly destroy the otherwise lingering Clairvoyance actor. Cleanup failure is logged without replaying the grant.
  A second copy waits once the world is fully revealed.
- Map preflight uses `TryGetNodeIndexForNextGoal`, default Combat types and `preferCloserToExit = true`, with the
  matching distance and main-modifier settings. Missing quest manager, empty map or no eligible node means pending.
  Cloak, Clairvoyance and both maps also wait in exit-boss rooms or invalid room/node state; there is no further node
  travel there, Hunter skips reset at world generation, and current-world map goals would be missed. Shards still land
  during a boss fight. Maps additionally wait in sidetracks and special-generated worlds, including Primus.
- Both maps wait for native map Treasures and map quests to finish `OnCreate` before another preflight. Inspect active
  map actors in `NetworkServer.spawned`; `ActorManager.allActors` registers them only after `OnCreate` returns.
  Cover both the Treasure and its child quest, including native shop purchases, without caching pending item/profile
  state or invoking private lifecycle methods. An initialized active quest permits additional maps; inactive and
  unrelated actors never block them. A missing actor manager leaves maps pending.
  The Treasure starts the actual vanilla quest, whose helper chooses the destination again.
- `QuestManager.StartQuest` does not deduplicate: multiple active maps are allowed, including maps of the same kind.
  Each quest stores its own goal and modifier IDs. Normal maps can share a node; Genuine maps avoid occupied main
  bonuses. Each copy must pass preflight and land separately. Shard effects also stack and use vanilla `[SaveActor]`.
- A Shard preventing a knockdown sends no DeathLink. A received DeathLink uses `Entity.Kill`, so a Shard can absorb
  it. After that call, clear DeathLink suppression if the hero has neither a knockout nor a knockout/bleed-out status
  effect. Retain it during bleed-out for the later knockdown event. The next normal knockdown after an absorbed kill
  sends normally.

### Curse traps

Issue #12. The three catalog `curse` items are opt-in traps: `Curse: Mild`, `Curse: Potent` and `Curse: Intense`.
They follow `traps`, independently of `in_run_items`. Only the local solo/host hero is cursed;
co-op guests are not directly given a curse status by the host's AP items. Native curse effects on other players,
including friendly fire, explosions and changed relations, remain intact, just as with a vanilla Hatred shrine.
Keep the full vanilla curse pool (including Dark Urge, Intermittent Explosion and Inductive Dream Affliction).
Delivery uses the shared Bound, connected, ready unconcluded run guard.

- Wait in every `ExitBoss` node, including Primus, and in `Room_Special_StarlessPath_BossPolaris` (a Special sidetrack
  node). A cleared boss room still waits until the player leaves it. Also wait during room transitions, without a
  current room/valid node or quest manager, or while the local hero is missing, inactive, dead, knocked out or bleeding out.
- `Shrine_Hatred.DoCurse` is private and its `_curses` pool is initialized by shrine `OnCreate`. Reproduce only its
  selection and initial fields through public vanilla APIs; create no shrine, reward or custom network message.
  Discover `CurseStatusEffect` prefabs with `FindAllByType(ResourceLoadSettings.Light)`, filter by
  `Dew.IsCurseIncludedInGame`, then resolve each through `AssetRef.asset` to load its full gameplay prefab, as the
  shrine does. Do not cache Unity assets across unloads or mutate shared prefabs.
- Filter by `availableStrengths`, `IsViable(localHero)` and positive `chanceWeight`, then call vanilla
  `Dew.SelectRandomWeightedInList`. An empty eligible pool leaves the trap pending. This avoids the helper's
  entry-zero fallback when every weight is zero. Each received copy rechecks viability; vanilla curse effects
  decide whether another copy is viable.
- Map Intense to `HatredStrengthType.Powerful` (enum value 4). Use `hero.CreateStatusEffect(prefab, hero,
  new CastInfo(hero, hero), beforePrepare)` to set `currentStrength` and vanilla `skillLevel = (int)(strength - 1)`.
  Half the time (`Random.value < 0.5`) the lift condition is Kills: Mild `28 + 2 × currentZoneIndex`, Potent
  `34 + 3 × currentZoneIndex`, Intense `50 + 3 × currentZoneIndex`. Otherwise it is Travel: 3 / 4 / 4 rooms.
  Preserve scaling through later loops and the vanilla skill-level expression.
- The vanilla status effect does everything else: networking, notification, lifting, removal and saving. A null
  creation result stays pending. A successful creation saves the shared applied counter and announces the tier and sender. Reconnect/restart never
  replays spent copies; traps may wait for a later room, world or run.

### Checks
- **Achievements:** when an achievement completes, send its check. **Suppress the vanilla unlock reward**, since the
  AP item replaces it. **Keep** the vanilla Stardust bonus (`DewAchievementItem.grantedStardust`, usually 50).
- **World clears:** detect the clear, then record it in the bound profile. Send that world's check for the current
  difficulty and every lower one, for the Traveler being played.
- **Souvenirs:** a postfix on `DewProfile.UnlockAccessory` sends the check when the loaded marked profile newly owns
  one of the 17 (a shop purchase, `UI_InGame_FloatingWindow_Shop.ClickMerchandise`, or a redeem code). The game's own
  unlock flag (`accessories[key].isUnlocked`) is the record: no AP record, no reward suppression. Like achievements,
  it always counts (Nap, Limbo, co-op, runs that Forced Lucid Dreams blocked). The mod logs the shop's filter at startup and warns when it differs
  from the data, so a souvenir added by a game update shows up (it sends nothing until the data is regenerated).
- **On every connect,** rebuild the full check list from the profile (completed achievements, recorded world
  clears, owned souvenirs, discovered artifacts and `AP:check:` records) and resend all of it. The server ignores duplicates.

### Received items
- Unlocks go through the game's own functions: `DewProfile.UnlockHero`, `UnlockSkill`, `UnlockGem`,
  `UnlockLucidDream`.
- Unlocks are **rebuilt from the full received-item list** on every connect. Receiving one twice changes nothing,
  so no item index is needed.
- **Repeatable rewards** (Stardust to `DewProfile.stardust`, mastery points to `DewProfileStats`) need a per-type **applied
  counter** stored in the profile. Store it so it is saved together with the currency it protects. Mastery lives in
  the separate `stats` file, so its counter has to be saved in that same write, or be made crash-safe some other way.
  Items that arrive several times, and server/cheat items (location −1/−2), count normally.
- **Timing mirrors vanilla.** Everything is written to the profile right away. Things that can only change between
  runs take effect then: the loot pool is built at run start, and Traveler, loadout and Lucid Dreams are chosen in the
  lobby. The notification shows "(next run)" when that applies.
- Mastery arrives as levels but the game stores points. Convert with `Dew.GetRequiredMasteryPointsToLevelUp`.
  There is no level cap. The mod adds the exact points for the next N levels, so partial progress is kept.
- **Where the records live.** New JSON fields would be dropped on the game's next save, so AP data goes into string
  lists the game only reads with `Contains`: `DewProfile.experienceFlags` holds the binding marker, the unlock record
  (`AP:unlock:<target>`), world clears (`AP:clear:<location key>`), wins (`AP:win:<Traveler>:<difficulty id>`), the
  Stardust counter (`AP:applied:STARDUST=<n>`), the forced Lucid Dreams (`AP:forced:<key>`) and the shuffled star
  requirements (`AP:star:<key>=<level>`). The mastery counters (`AP:applied:MASTERY_<Traveler>=<n>`) live in
  `DewProfileStats.recoveredLossPoints`, in the `stats` file, so each is written in the same save as the value it
  protects.
- **Known limitation: the game's stats recovery.** At startup, before mods load, the game compares
  the `stats` file with its daily backups. If total mastery or play time went down, it adds the lost mastery levels back
  (`DewProfileStats.GetRecoveryDelta`) but not our mastery counters, so Mastery items applied between that backup and
  the loss get applied a second time. There's no exact repair: that needs the backups' counters. The over-grant is
  bounded mastery, so the mod only reports it: each new `loss_…` entry the game adds to `recoveredLossPoints` after
  binding logs a warning and shows a notice once (`AP:seenloss:<entry>`; entries from before the binding are marked seen
  at bind time).
- **The unlock record.** `DewProfile.Validate` runs on every profile load and re-derives unlocks from achievements: it
  locks the target of every incomplete achievement and unlocks the target of every completed one. `UnlockHero` also
  unlocks a Traveler's alternate memories whose achievement is complete. On a marked profile the mod therefore keeps
  its own record of what AP has granted, and after `Validate`, on mod load, on connect and after items it sets every
  managed unlock (every Traveler, the 93 targets, plus a locked Traveler's own memories) to match, using the game's
  `Unlock*`/`Lock*` functions. This works offline too.
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
   on the title screen (`Title` scene, no current online lobby, Mirror client/server inactive, no `GameManager`): a
   lobby's Travelers and a run's loot pool were built from the unbound unlocks.
3. The mod refuses to bind a profile that isn't fresh: it must have no completed achievements and no runs played. There
   is also a deliberate override for recovery (`ap_bind_force`). The test: no completed achievements, no recorded
   game results (`lastGameResults`, `recentlyConcededGames`) and a total Traveler play count of 0.
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
   `SteamUserStats` calls). The fresh profile would otherwise overwrite Steam's stat progress with lower values.
   `SyncAchievements` is skipped, and `SteamUserStats.SetStat`/`SetAchievement` are blocked while a marked profile is
   loaded, which covers every caller.
9. Connection details (host:port, slot, password) are stored as **editable settings**, separate from identity.
   archipelago.gg rooms can change port.

### Offline play
Allowed. A marked profile plays offline with the items it has already received. Checks are recorded in the profile
and sent on reconnect. An **always-visible "OFFLINE — checks will send on reconnect" indicator** is shown.

### Reconnecting
A logged-in, bound session that loses its connection retries on its own (AP client requirement). Binding never retries.
- **Detection:** an abrupt server stop aborts MultiClient.Net's socket without `SocketClosed` (only `ErrorReceived`), so
  `Pump` also treats `Socket.Connected` turning false on a Connected session as a drop.
- **Retries** back off 2, 5, 10, 30, then every 60 s, without limit, and play on offline in between. Each retry is a
  normal connection (new attempt, seed check, login, `LoggedIn` rebuild) with the **dropped session's** server, slot
  and password, not the mod config: if a room moves port, the player sets the new server and types `ap_connect`. A
  retry gives up after 15 s (`ConnectAsync` never completes if no RoomInfo arrives).
- **A failure to reach the server** (connect error or timeout, closed socket, a `LoginFailure` without error codes)
  schedules the next retry. **A refusal stops them:** a different seed, a `LoginFailure` with error codes (slot,
  password), the slot_data checks, a profile change. So does anything that disconnects: `ap_connect`, `ap_disconnect`,
  a profile switch, unloading. Reloading the same bound profile retries at once.
- The indicator shows the countdown and attempt number; `ap_status` shows the reconnect state.

### Co-op
Each player's own profile, mod and slot are independent. Jonas's Wares and in-run item delivery require solo/host
play; pending in-run items wait for it. The shared loot pool is the union of the players' unlocks, as in vanilla.
All other checks also work on a joining client, even with a non-AP host.

### DeathLink (off by default)
- **Send** when your Traveler is knocked down (`ClientEventManager.OnHeroKnockedOut` /
  `Hero.ClientHeroEvent_OnKnockedOut` for the local hero).
- **Receive:** kill your Traveler. This ends the run only in solo. In co-op it's a normal knockdown that teammates can
  revive. Use `Entity.Kill()`, which goes through the hero's normal death interrupt (a knockdown, or a bleed-out
  first on difficulties with `enableBleedOuts`).
- **Known limitation: a player who *joined* someone else's co-op game can't receive DeathLinks.**
  `Entity.Kill` is server-only and the game has no client command to kill your own Traveler, so the mod on a joining
  client can't apply one. It shows the DeathLink with "(can't be applied: only the host can kill Travelers)" and does
  nothing else. Joining players still *send* DeathLinks.
- A knockdown caused by a received DeathLink does not send a new one. Known bug: a DeathLink received while
  already bleeding out from a normal hit can suppress that knockdown's outgoing DeathLink.
