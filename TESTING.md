# In-game test checklist

Claude can build and generate but can't play, so these checks need a person at the game. Each step lists what to do
and the `[AP]` lines to look for in the game log:

```
%USERPROFILE%\AppData\LocalLow\Lizard Smoothie\Shape of Dreams\Player.log
```

(`Player-prev.log` is the previous session.) Searching the log for `[AP]` finds every mod line. Please send back the
whole log, or at least every `[AP]` line, plus anything in the "Report" lines below.

**Never test on your main profile.** Everything below uses throwaway profiles.

## 0. Setup

1. Build and deploy the mod: `dotnet build mod/SoDArchipelago.sln` (copies it to `<game>/Mods/SoDArchipelago/`).
2. Archipelago checkout (0.6.8) with a Python 3.13 venv, as in AGENTS.md "Commands". Copy `apworld/shape_of_dreams` into
   its `worlds/` folder (or install `dist/shape_of_dreams.apworld`).
3. Put this YAML in the checkout's `Players/` folder (and nothing else):

   ```yaml
   name: SoDTest
   game: Shape of Dreams
   Shape of Dreams:
     goal_difficulty: deep_sleep
     goal_traveler_count: 1
     death_link: true
   ```

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
   - `[AP] Resolved Archipelago.MultiClient.Net, ... -> Archipelago.MultiClient.Net_<ticks>, ...` (the mod loader renames
     mod assemblies; this proves the resolver works)
   - **Report** whether a `[AP] Resolved Newtonsoft.Json, Version=11.0.0.0 ...` line appears (AGENTS.md item 2).
   - `[AP] Room seed <seed>; wanted marker Archipelago:<seed>:SoDTest; loaded profile 'AP Test' marker <none>`
   - `[AP] Fresh-profile check: completed achievements=0, recorded runs=0, Traveler play count=0, ...`
   - `[AP] Profile 'AP Test' isn't bound yet. Type ap_bind ...`
   - If instead it says the profile isn't fresh, **report the fresh-profile line** (e.g. whether the tutorial counts).
3. `ap_bind`.
   - `[AP] Bound profile 'AP Test' (...) to Archipelago:<seed>:SoDTest`
   - `[AP] Connected: seed <seed>, slot SoDTest (profile 'AP Test').`
   - `[AP] slot_data: data_format_version=2, ..., death_link=True`, `[AP] DeathLink enabled.`
   - `[AP] Resending 0 checks (0 achievements, 0 world clears)`
   - `[AP] Goal: 0/1 Travelers have won at rank 1+ ()`
   - On screen: `Archipelago: SoDTest` top-left. This proves `wss://`-then-`ws://` connection works under Unity Mono
     (AGENTS.md item 3). To test `wss://` too, connect once to a real archipelago.gg room (any seed with a Shape of
     Dreams slot) on a second throwaway profile.
4. **Guard: wrong profile.** Switch to another (unbound) profile in profile selection.
   - `[AP] Profile loaded: '<other>' ... marker=<none>` then `[AP] Profile switched, so Archipelago disconnected...`
   - `ap_connect` on that profile must **not** log in: it asks for `ap_bind` (fresh) or refuses (not fresh). Don't bind
     it; switch back to "AP Test".
   - `[AP] Archipelago profile loaded (offline). Type ap_connect to connect.` and the red
     **OFFLINE — checks will send on reconnect** notice.
5. **Guard: other seed.** (Optional) Generate a second seed, host it, `ap_connect` while "AP Test" is loaded.
   - `[AP] ... The loaded profile 'AP Test' belongs to a different seed/slot (...)` and no login.
6. **Steam sync.** After loading "AP Test": `[AP] Skipped the Steam achievement sync (Archipelago profile)`.
7. Reconnect: `ap_connect` on "AP Test" logs straight in (no bind prompt).

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
   - `[AP] Check: world 1 cleared as Hero_Lacerta on diffNormal: World 1 Clear (Deep Sleep): Lacerta`
   - The run's very first zone logs `from=''` and sends nothing.
3. **Report** every `Zone loaded` line of a full run, including world 5 → the ending (Pure White Dream is a separate
   zone, `Zone_Primus`, so leaving world 5 should log `zoneIndex=5` and send the World 5 clear). If you loop instead,
   the next world should also be `zoneIndex=5`, and world 6 onwards must log `No world clear ... loops send nothing`.
4. On a harder difficulty (Ominous Dream or Nightmare; logic expects Lacerta copies for those, but the mod never blocks
   you), one clear sends that difficulty and every lower one: e.g. `World 1 Clear (Nightmare): Lacerta,
   World 1 Clear (Ominous Dream): Lacerta, World 1 Clear (Deep Sleep): Lacerta`.
5. Continue a saved run (quit mid-run, continue): the reload logs `fromSave=True` and sends nothing.

## 6. Goal

1. Win the Deep Sleep run (reach the Pure White Dream or the Starless Path).
   - `[AP] Game concluded: result=PureWhiteDream difficulty=diffNormal (...) hero=Hero_Lacerta visitedWorlds=...`
   - `[AP] Recorded win: Hero_Lacerta on diffNormal`, `[AP] Goal: 1/1 ...`, `[AP] Goal complete!`
   - The server shows the goal.
2. **Report** any `result=UnknownFate` (the code says it only exists in demo builds).

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
