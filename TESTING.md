# In-game test checklist

Claude can build and generate but can't play, so these checks need a person at the game. Each step lists what to do
and the `[AP]` lines to look for in the game log:

```
%USERPROFILE%\AppData\LocalLow\Lizard Smoothie\Shape of Dreams\Player.log
```

(`Player-prev.log` is the previous session.) Searching the log for `[AP]` finds every mod line. Please send back the
whole log, or at least every `[AP]` line, plus anything in the "Report" lines below.

**Never test on your main profile.** Everything below uses throwaway profiles.

Results so far (2026-09-27) and the list of what is still untested: AGENTS.md "Status".

## 0. Setup

1. Build and deploy the mod: `dotnet build mod/SoDArchipelago.sln` (copies it to `<game>/Mods/SoDArchipelago/`).
2. Archipelago checkout (0.6.8) with a Python 3.13 venv, as in AGENTS.md "Commands". Copy `apworld/shape_of_dreams` into
   its `worlds/` folder (or install `dist/shape_of_dreams.apworld`).
3. Copy [`testing/SoDTest.yaml`](testing/SoDTest.yaml) into the checkout's `Players/` folder (and nothing else). It's
   the baseline: slot `SoDTest`, one Deep Sleep win for the goal, DeathLink and shuffled star requirements on, no forced
   dreams. Never edit it in the repo; a test that needs other options changes the copy.

4. Generate and host it:

   ```sh
   python Generate.py --seed 1
   python MultiServer.py output/AP_<number>.zip
   ```

   The server listens on `localhost:38281`. Keep its window open: you'll type `/send` commands there.

## 1. Mod loads (AGENTS.md "Not yet verified" 1-5)

1. Start the game, enable **Archipelago** in the mod manager if needed.
   - `[AP] Loaded com.sodarchipelago.archipelago 0.2.0; data format 2, extracted from game r.1.4.0.13_s; running game ...`
   - No `Failed to load com.sodarchipelago.archipelago` line from `[DewMod]`.
2. Open the mod's config in the mod manager. **Report:** do Server / Slot / Password fields show up and save?
3. Open the console and type `ap_status`.
   - `[AP] Disconnected; server=... slot=...; profile '...' marker=<none> ...` (proves no-parameter commands work).
4. Type `ap_server localhost:38281`, then `ap_slot SoDTest` (proves commands with a parameter work).
   - `[AP] Config: server = localhost:38281`, `[AP] Config: slot = SoDTest`

**Report these startup lines** (they answer DESIGN.md's open questions):
- `[AP] Difficulty ids -> display names: diffTutorial='...' diffEasy='...' diffNormal='...' diffHard='...' diffNightmare='...' diffLimbo='...'`
  The apworld assumes diffEasy = Nap, diffNormal = Deep Sleep, diffHard = Ominous Dream, diffNightmare = Nightmare.
- `[AP] Build content: zoneCountByTier=[...] (worlds per loop = N)` (expected 5)
- `[AP] Star slots (default/max) per Traveler: ...` and `[AP] Star slots: N buyable slots cost X Stardust in total`
  (the Stardust buffer is 1,525 by default)
- `[AP] Mastery levels: ...` (only logged when the game starts with a bound profile loaded; the mod never reads an
  unbound profile)

## 2. Profile binding and guard

1. Title screen → profile selection → **create a new profile** (e.g. "AP Test"). Don't play yet.
   - `[AP] Profile created: 'AP Test' path=... marker=<none>`
2. Console: `ap_connect`.
   - `[AP] Connecting to localhost:38281 as SoDTest...`
   - **Report** whether a `[AP] Resolved Newtonsoft.Json, Version=11.0.0.0 ...` line appears (AGENTS.md item 2; none is
     expected, since MultiClient.Net is merged into our DLL and Mono binds its Newtonsoft 11 references on its own).
   - `[AP] Room seed <seed>; wanted marker Archipelago:<seed>:SoDTest; loaded profile 'AP Test' marker <none>`
   - `[AP] Fresh-profile check: completed achievements=0, recorded runs=0, Traveler play count=0, ...`
   - `[AP] Profile 'AP Test' isn't bound yet. Type ap_bind to log in and bind it ...`
   - If instead it says the profile isn't fresh, **report the fresh-profile line** (e.g. whether the tutorial counts).
3. **Bind needs a working login.** First `ap_slot WrongName`, `ap_connect`, `ap_bind`:
   - `[AP] Logging in; the profile is bound once the login succeeds.` then `[AP] Login failed: ... Nothing was bound.`
   - `ap_status` still shows `marker=<none>`. Set the right slot (`ap_slot SoDTest`) and `ap_connect` again.
4. `ap_bind`.
   - `[AP] Bound profile 'AP Test' (...) to Archipelago:<seed>:SoDTest`
   - `[AP] Connected: seed <seed>, slot SoDTest (profile 'AP Test').`
   - `[AP] slot_data: data_format_version=3, data_hash=<hash>, ..., death_link=True`, `[AP] DeathLink enabled.`
   - `[AP] Resending 0 checks (0 achievements, 0 world clears)`
   - `[AP] Goal: 0/1 Travelers have won at rank 1+ ()`
   - On screen: `Archipelago: SoDTest` top-left. This proves `wss://`-then-`ws://` connection works under Unity Mono
     (AGENTS.md item 3). To test `wss://` too, connect once to a real archipelago.gg room (any seed with a Shape of
     Dreams slot) on a second throwaway profile.
   - **Report** whether binding was refused with `Binding only works on the title screen` even though you were on the
     title screen. The `[AP] Bind refused: scene=... networkClient=... networkServer=... onlineLobby=... run=...` line
     shows which check fired. (The first in-game test hit this: the check treated the always-present `LobbyManager` as a
     lobby. Fixed 2026-09-27.)
5. **Bind is refused outside the title screen.** On a second fresh profile: `ap_connect`, open a lobby, `ap_bind`
   → `Binding only works on the title screen, not in a lobby or a run.` Go back to the title screen and load "AP Test".
6. **Guard: wrong profile.** Switch to another (unbound) profile in profile selection.
   - `[AP] Profile loaded: '<other>' ... marker=<none>` then `[AP] Profile switched, so Archipelago disconnected...`
   - `ap_connect` on that profile must **not** log in: it asks for `ap_bind` (fresh) or refuses (not fresh). Don't bind
     it; switch back to "AP Test".
   - `[AP] Archipelago profile loaded (offline). Type ap_connect to connect.` and the red
     **OFFLINE — checks will send on reconnect** notice.
7. **Guard: other seed.** (Optional) Generate a second seed, host it, `ap_connect` while "AP Test" is loaded.
   - `[AP] ... The loaded profile 'AP Test' belongs to a different seed/slot (...)` and no login.
8. **Steam sync.** After loading "AP Test": `[AP] Skipped the Steam achievement sync (Archipelago profile)`.
9. Reconnect: `ap_connect` on "AP Test" logs straight in (no bind prompt).
10. **Abandoned connect.** `ap_server 10.255.255.1:38281` (an address that doesn't answer), `ap_connect`, then right
    away `ap_disconnect`. No `Connected`/`Room seed` line appears later, and no error follows. Put the server back.

## 3. Received items

In the **server window**:

1. `/send SoDTest Progressive Aurena`
   - `[AP] Received Progressive Aurena (Aurena) (server)`
   - `[AP] Unlock state enforced (received items) on 'AP Test': +Hero_Aurena`
   - Aurena is selectable in the lobby, with her base memories. Her alternate memories stay locked.
2. `/send SoDTest Progressive Aurena` again → `(Reduction)`, `+St_Q_Reduction`; Reduction is available in her Q slot.
3. `/send SoDTest Stardust` → `[AP] Received Stardust (+650) (server)` and
   `[AP] Stardust +650 (1 x 650); now N; applied 1`. The Stardust counter in the lobby goes up by 650.
4. `/send SoDTest Mastery: Lacerta` →
   `[AP] Mastery Hero_Lacerta: +5 levels (... points), level a -> a+5 (points into level p); applied 1`.
   **Report** the mastery level shown in the lobby.
5. `/send SoDTest Memory: Blizzard` and `/send SoDTest Lucid Dream: Bon Voyage` → `+St_L_Blizzard`,
   `+LucidDream_BonVoyage`. Bon Voyage is selectable in the lobby.
6. **Crash safety / no double-apply:** `ap_disconnect`, `ap_connect`. No new "Received" lines, no second Stardust or
   Mastery application (counters say `applied 1`).
7. **Restart persistence:** quit the game, start it, load "AP Test" (don't connect).
   - Aurena and her Reduction are still unlocked (the game's `Validate` would normally re-lock them;
     `[AP] Unlock state enforced (mod load|profile validate) ...` lines show the mod putting them back, if needed).
8. **Mid-run:** start a run, then `/send SoDTest Memory: Chain Lightning`.
   - `[AP] Received Memory: Chain Lightning (server) (next run)`.

## 4. Achievement checks and reward suppression

1. In a run, complete an easy achievement, e.g. **Novice Treasure Hunter** (open a treasure chest).
   - The game's own `Achievement completed: ACH_NOVICE_TREASURE_HUNTER`
   - `[AP] Suppressed vanilla achievement unlock of Gem_R_Adventure (an AP item unlocks it)`
   - `[AP] Blocking Steam stat/achievement writes while an Archipelago profile is loaded (first: ...)`
   - `[AP] Check: Achievement: Novice Treasure Hunter (ACH_NOVICE_TREASURE_HUNTER)`
   - The server window shows the check. The achievement's +50 Stardust still arrives.
   - Essence of Adventure stays **locked** (unless the AP item for it was received).
2. **Report:** did your Steam achievement for it change? (It must not.)

## 5. World clears (difficulty ids, zone index)

1. Start a run on **Deep Sleep** as Lacerta. On the lobby → run: `[AP] Run ready: difficulty=diffNormal (...) bleedOuts=... hero=Hero_Lacerta host=True marked=True bound=True`.
2. Clear world 1 and travel to world 2.
   - `[AP] Zone loaded: from='Zone_...' to='Zone_...' traveling=True fromSave=False zoneIndex=1 loop=0 difficulty=diffNormal (...) hero=Hero_Lacerta`
   - `[AP] Check: world(s) 1 cleared as Hero_Lacerta on diffNormal (moved on): World 1 Clear (Deep Sleep): Lacerta`
   - The run's very first zone logs `from=''` and sends nothing.
   - At startup, `[AP] Build content: zoneCountByTier=[...] (worlds per loop = 4)` must say 4.
3. **Report** every `Zone loaded` line of a full run. Beating world 4's boss and taking the Dream rift must log
   `to='Zone_Primus' ... zoneIndex=4` and send **World 4** (not World 5). Nothing is sent in Primus (shop, door, boss).
   Touching the white light ends the run: see section 6 for the World 5 clear. If you loop instead, loop 2's first world
   is also `zoneIndex=4` (World 4), and leaving it must log `No world clear: left world 5 ...` (it isn't Primus).
4. On a harder difficulty (Ominous Dream or Nightmare; logic expects Lacerta copies for those, but the mod never blocks
   you), one clear sends that difficulty and every lower one: e.g. `World 1 Clear (Nightmare): Lacerta,
   World 1 Clear (Ominous Dream): Lacerta, World 1 Clear (Deep Sleep): Lacerta`.
5. Continue a saved run (quit mid-run, continue): the reload logs `fromSave=True` and sends nothing.

## 6. Goal

1. Win the Deep Sleep run (reach the Pure White Dream or the Starless Path).
   - `[AP] Game concluded: result=PureWhiteDream difficulty=diffNormal (...) hero=Hero_Lacerta visitedWorlds=...`
   - Pure White Dream: `[AP] Check: world(s) 5 cleared as Hero_Lacerta on diffNormal (Pure White Dream): World 5 Clear
     (Deep Sleep): Lacerta`. Starless Path instead: `world(s) 1,2,3,4,5 cleared ... (Starless Path)` with all five.
   - `[AP] Recorded win: Hero_Lacerta on diffNormal`, `[AP] Goal: 1/1 ...`, `[AP] Goal complete!`
   - The server shows the goal.
2. Die in a run (or concede): `[AP] Game concluded: result=GameOver ...` (or `Conceded`) and **no** `Check:` line.
3. **Report** any `result=UnknownFate` (the code says it only exists in demo builds).

## 7. Offline play

1. `ap_disconnect`. The red OFFLINE notice shows.
2. Complete an achievement or a world clear.
   - `[AP] Check: ... - offline, sent on reconnect`
3. `ap_connect`: `[AP] Resending N checks (...)` includes it, and the server shows it.

## 8. DeathLink (host / solo)

Run the helper from the Archipelago venv, on the same slot (a second client on a slot is allowed):

```sh
python -u tools/deathlink_test.py ws://localhost:38281 SoDTest
```

1. In a run, get knocked down.
   - `[AP] Local Traveler knocked out; sending DeathLink.`; the helper prints `DeathLink received: ...`.
2. Revive or start a new run, then press Enter in the helper.
   - `[AP] DeathLink: SoDTest pressed Enter`, your Traveler goes down (or bleeds out first on difficulties with
     bleed-outs; the `Run ready` line says `bleedOuts=`), and then
     `[AP] Knocked out by a received DeathLink; not sending one back.` The helper must **not** print a new DeathLink.
3. Outside a run: `[AP] DeathLink: ... (not in a run, ignored)`.

## 9. Co-op (DESIGN.md "Co-op" (verify), needs two game copies)

Both players use their own bound profile and slot. As the **joining** player, check that achievements
(`[AP] Check: Achievement: ...`) and world clears (`[AP] Zone loaded ... zoneIndex=...` + `[AP] Check: world ...`) are
logged on the joining client. A DeathLink received by the joining player logs
`(can't be applied: only the host can kill Travelers)` and must leave their Traveler untouched (a known limitation,
DESIGN.md "DeathLink"). A knockdown of the joining player's Traveler must still send a DeathLink.

## 10. Live reload

Disable and re-enable the mod in the mod manager while on the title screen: `[AP] Unloaded`, then a new `[AP] Loaded`
with no errors, and `ap_connect` still works.

## 11. Forced Lucid Dreams (DESIGN.md "Forced Lucid Dreams")

Needs a seed with forced dreams, e.g. a YAML with `forced_evil_lucid_dreams: ["Grievous Wounds"]` and
`forced_chaotic_lucid_dreams: ["WILD"]`, on a new profile.

1. `ap_connect`: `[AP] Forced Lucid Dreams: Grievous Wounds, WILD (released: )`.
2. Host a lobby (normal mode): the feed shows `Archipelago forces these Lucid Dreams on until you receive them: ...`,
   and both dreams are active (with the locked icon). Clicking one doesn't turn it off.
3. Start a run: `[AP] Forced Lucid Dreams this run: ... missing: none`. Clearing world 1 sends its check.
4. `/send SoDTest Lucid Dream: WILD`: `Received Lucid Dream: WILD - no longer forced`. In the next lobby WILD can be
   turned off. Start a run without it: `missing: none` (WILD is released), and only Grievous Wounds is forced.
5. Joining player (co-op): join a lobby whose host doesn't have Grievous Wounds on. At run start the feed shows
   `Forced Lucid Dreams missing (Grievous Wounds): world clears and wins won't count this run.`, and leaving world 1
   logs `Not counted (world 1 clear): ...` and sends nothing. Achievements still send.
6. Limbo lobby: no forcing, no start-condition message.
7. Profile check: the Limbo mode stays locked on a profile with 4+ forced Evil dreams and no released ones.

## 12. Shuffled star requirements (DESIGN.md "Shuffled star requirements")

Needs a seed with `shuffle_star_requirements: true`, on a new profile. The `[AP] slot_data:` line lists every star's
level under `star_requirements`; compare a few with the vanilla levels in `RawData/en-US/stars.json` (`requiredLevel`).

1. `ap_connect`: `[AP] Star requirements: shuffled for 305 stars (...)`.
2. Lobby → Constellations: the level on each star icon, the locked look, the list order and the "requires mastery"
   text on a locked star all show the seed's levels. A star at 0 in the seed can be bought on the fresh profile; a vanilla
   level-0 star that the seed moved up can't.
3. `/send SoDTest Mastery: Lacerta` (+5 levels): Lacerta's stars at 5 or less in the seed unlock.
4. Reload: play into a run, go back to the lobby (the game unloads and reloads prefabs): the levels are still the seed's.
5. Offline: `ap_disconnect`, back to the title screen and into a lobby: still the seed's levels.
6. Switch to an unbound profile: `[AP] Star requirements: vanilla`, and the constellation shows vanilla levels. Switch
   back: shuffled again.
7. Live reload: disable the mod in the Mod Manager: vanilla levels; enable it: shuffled again.
8. Co-op (non-AP joiner): the joiner's constellation shows vanilla levels.

## 13. Co-op session: host + non-AP friend, forced dreams and shuffled stars

One session that covers a winning run and the goal [5.3, 6.1], co-op with a friend who doesn't run Archipelago [9],
Forced Lucid Dreams [11] and shuffled star requirements [12]. You host on this PC; the friend joins from their own PC
with no mods. Not covered: an AP player joining someone else's lobby, and two AP players in one lobby.

### Setup

1. Build and deploy the current mod: `dotnet build mod/SoDArchipelago.sln`. The v0.2.0 build doesn't have forced dreams
   or the star shuffle. Don't also subscribe to the Workshop item on this PC.
2. A new seed, because the "AP Test" seed has neither option. Copy `apworld/shape_of_dreams` into the checkout's
   `worlds/`, copy [`testing/SoDTest.yaml`](testing/SoDTest.yaml) over `Players/SoDTest.yaml`, and in the copy set:

   ```yaml
     forced_evil_lucid_dreams: ["Grievous Wounds"]
     forced_chaotic_lucid_dreams: ["Harmless Whispers"]
   ```

   Generate and host it as in step 0. The old seed's profile "AP Test" can't connect to it (it's bound to the old seed).
3. In the game: create a new profile, **"AP Coop"**, then `ap_connect` and `ap_bind`.
   - `[AP] Bound profile 'AP Coop' ...`, `Connected: seed ...`
   - `[AP] Forced Lucid Dreams: Grievous Wounds, Harmless Whispers (released: )`
   - `[AP] Star requirements: shuffled for 305 stars (...)`
   - The `[AP] slot_data:` line lists `star_requirements`; keep it for step B.
4. Optional, for DeathLink: `python -u tools/deathlink_test.py ws://localhost:38281 SoDTest` in the venv.

### A. Stars, solo, before the friend joins [12.2, 12.3]

1. Lobby → Constellations. Pick three stars and compare with the slot_data line: the number on the icon, the locked
   look, the list order and the "requires mastery" text use the seed's levels. **Report** any star that shows its vanilla
   level (`requiredLevel` in `RawData/en-US/stars.json`) instead.
2. Buy a star that is 0 in the seed; try a vanilla level-0 star that the seed moved up (it must be locked).
3. `/send SoDTest Mastery: Lacerta`: Lacerta's stars at 5 or less in the seed unlock. Equip one for the run.

### B. Host the lobby, friend joins [11.2, 12.8, 9]

1. Host a lobby (normal mode, Deep Sleep). The feed shows `Archipelago forces these Lucid Dreams on until you receive
   them: Grievous Wounds, Harmless Whispers`. Both are active with the locked icon; clicking one doesn't turn it off.
2. The friend joins. **Report:** does the friend's constellation show vanilla levels? (It must.)
3. **Report** which Memories/Essences drop during the run that "AP Coop" hasn't unlocked (the loot pool is the union
   of both players' unlocks, so the friend's unlocks can drop for you too).

### C. The run [5, 8, 9, 11.3]

1. Start: `[AP] Run ready: ... host=True marked=True bound=True` and
   `[AP] Forced Lucid Dreams this run: ... missing: none`.
2. Each world you leave sends `[AP] Check: world(s) N cleared as <Traveler> on diffNormal (moved on): ...`.
   **Report** every `Zone loaded` line. The Dream rift after world 4's boss must log `to='Zone_Primus' ... zoneIndex=4`
   and send World 4, and nothing is sent inside Primus.
3. Achievements either player triggers for you send `[AP] Check: Achievement: ...`.
4. DeathLink: your knockdown logs `Local Traveler knocked out; sending DeathLink.` The **friend's** knockdown sends
   nothing. Enter in the helper knocks your Traveler out (`Knocked out by a received DeathLink; not sending one back.`).
5. Mid-run, `/send SoDTest Lucid Dream: Harmless Whispers`:
   `[AP] Received Lucid Dream: Harmless Whispers - no longer forced (server) (next run)`. The run still counts.

### D. Win and goal [5.3, 6.1]

1. Pure White Dream: `[AP] Game concluded: result=PureWhiteDream ...`, `World 5 Clear (Deep Sleep): <Traveler>`,
   `[AP] Recorded win: ...`, `[AP] Goal: 1/1 ...`, `[AP] Goal complete!`, and the server shows the goal.
   Starless Path instead: `world(s) 1,2,3,4,5 cleared ... (Starless Path)` and the same win and goal lines.
2. A lost run instead: `result=GameOver` and no `Check:` line. Try again.

### E. Back in the lobby [11.4, 12.4]

1. Harmless Whispers can now be turned off; Grievous Wounds is still forced. Start a run without Harmless Whispers:
   `missing: none`.
2. Constellations still show the seed's levels (the game reloaded the stars when the lobby opened).

### Optional, if there's time

- A clear on Ominous Dream or Nightmare sends that difficulty and every lower one [5.4].
- Quit mid-run and continue it: `fromSave=True` and nothing sent [5.5].
- `ap_disconnect`, clear a world, `ap_connect`: the clear is resent [7].
- Switch to an unbound profile: `[AP] Star requirements: vanilla` [12.6].
