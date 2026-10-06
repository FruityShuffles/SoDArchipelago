# Solo test: issues #7–#12

Scope: commits `f06dbef`, `923137d`, `ae009a8`, `251effd`, `e33f4e1`, `7e0e1d9`,
and review/classification fixes in `56237e8` and `6143faa`. One solo test seed; additional runs on that seed
may be needed for random shrines, quests and artifacts. No co-op coverage or claims.

## Preparation

Prepared on 2026-10-04: build/deployment passed with zero warnings/errors; the 12 pool scenarios and generated
seed checks passed. Seed `AP_44140295405363709462.zip` starts with Cetus and Shell. The deployed DLL matches the
merged build, data format 5, hash `fbb7bcce1eae8c6c28e8e5897c08f46864789070b29a034dca88783c6f0842c0`.
Local connection details and artifact paths are in `.claude/solo-issues-7-12/SESSION.md`.
Testing started on 2026-10-04: the player created and bound **AP 7-12** as **SoDTest**. Cetus and Shell unlocked.
An initial grant of 20 Stardust packs applied once: +13,501, observed balance 13,631, applied counter 20.
The lobby test grant added one Blessing: Pure Dream and one Cloak of Guidance. The server confirms one copy
each (24 received items total). Neither delivered in the lobby; both survived a game restart and delivered
once on entering solo Deep Sleep as Cetus. The player confirmed both effects; logs contain one notice each.
No Curses have been granted yet. Remaining in-game checks are pending.

- Build and deploy the current mod locally. Keep a backup of the previous deployed mod for old seeds.
- Copy the current world into the AP 0.6.8 checkout. Preserve its existing player YAMLs and server saves.
- Generate seed `20261004` using only [solo-issues-7-12.yaml](solo-issues-7-12.yaml).
- Host on `localhost:38281` with normal collection settings. Commands go through a dedicated `cmds.txt`;
  don't send an entire command deck at once. Goal completion is outside this test.
- Keep seed output, command decks and server logs under `.claude/solo-issues-7-12/`.
- Create a new game profile named **AP 7-12 Solo**. Do not use existing profiles or `ap_bind_force`.
  Set server `localhost:38281`, slot `SoDTest`, no password; `ap_connect`, then `ap_bind` on the title screen.
- Enable only Archipelago Randomizer for this test. Enter **Dream Alone** on Deep Sleep.

The YAML deliberately uses 54,001 Stardust to exercise remainder allocation. It enables all new content,
disables passive mastery and enables DeathLink for the Shard regression. It has no forced dreams.
The goal stays Nightmare with all nine Travelers, so Deep Sleep testing will not complete it.
No items have been pre-sent: first observe the fresh profile and its two starting Travelers.

## Preflight: generation and compatibility (#7)

- [x] The generated seed has 302 checks: 93 achievements, 135 world clears, 17 souvenirs, 30 wares,
  9 shrines, 6 quests and 12 artifacts. Deep Sleep clears are priority; souvenirs and artifacts are excluded.
- [x] Pool: 34 progression, 131 useful, 126 filler and 11 traps. Of the filler, 80 are Stardust and
  46 are Blessings/Treasures. All 72 Mastery items are useful and none is on an excluded location.
- [x] Stardust allocation is 676 for the first pack, then 675 for the other 79: exactly 54,001 total.
- [x] Mod and slot data agree on data format 5 and `data_hash`. Fresh binding succeeds; no unknown IDs or targets.
- [x] Automated pool checks cover all four in-run/trap combinations and ware counts 0, 30 and 100.
  Stardust total is preserved; no new item kinds appear when both toggles are off.

Record the generated seed name, starting Travelers, build/hash and server options in the local session record.
Prior automated results: 580 AP/general tests and 682 C# assertions passed. Those do not establish in-game results.

## A. Lobby, delivery and persistence (#7)

- [x] Reported connected slowdown resolved: after removing the delivery loop's missing-manager scene searches
  and reloading the mod, the player confirmed smooth frame pacing (2026-10-04). 686 assertions pass.
  Continue watching frame pacing on reconnect and return-to-title during the remaining tests.
- [x] In the lobby, send one Blessing and one Treasure. Neither applies or announces delivery yet.
  Sent Pure Dream and Cloak of Guidance once each; server receipt and absence of lobby delivery logs verified.
  Log archived locally before the restart step. Both original copies later delivered in the run.
- [x] Restart the game before starting the run. Reconnect on the same profile: both remain pending.
  Player restarted/reconnected in the lobby. Log confirms the same bound profile and no delivery notices;
  archived locally. Both copies subsequently delivered in the run.
- [x] Start a solo run. Each lands once when usable, with the correct item and source in the feed.
  Solo Deep Sleep, Cetus, Zone_Forest: one Pure Dream notice and one Cloak of Guidance notice, both from server.
  Player confirmed both effects; no exception lines. Log archived locally.
- [x] Send a second copy of an item already delivered. It produces a second effect; reconnecting alone does not.
  In-run reconnect passed: original Pure Dream and Cloak notices remained one each, with no exceptions.
  Then sent one additional Pure Dream: its notice count rose to two; Cloak stayed at one. Player confirmed
  the second map marker. Both logs archived locally.
- [x] Disconnect before a queued map item has a valid target. Travel offline: it remains pending.
  Reconnect in a suitable room: it lands once.
  Disconnected in the Forest boss room with five map effects pending; traveled to LavaLand (world 2).
  None delivered offline. Reconnected in world 2: Maw, Cloak, Clairvoyance and both maps each delivered once.
  Shard stayed at one delivery; zero exceptions. Both native map quests started.
- [x] Receive a server grant while offline; travel offline, then reconnect: it delivers once.
  Sent one Treasure Map after confirmed disconnection. Server has one copy (26 items total); game log has
  no map delivery notice or exceptions, including after verified offline travel to Room_Forest_Combat_0_2.
  Reconnected: exactly one Treasure Map notice, followed by Quest_TreasureMap starting; player confirmed
  delivery and marker. Pure Dream stayed at two notices, Cloak at one; zero exceptions. This checks offline receipt;
  deferral of an item already received by the mod remains a separate test above.
- [x] Save by clearing/traveling to another room, quit and continue. Delivered effects/counters survive;
  reconnect does not replay them. Player confirmed correct restoration. Log shows Treasure Map quest
  LoadedFromSave, fromSave=True, no repeat delivery notices and no exceptions.
- [x] Leave an item pending at a world's end and verify later-world delivery.
  Five effects carried from the Forest boss room to LavaLand while offline; each delivered once on reconnect.
- [x] A pending map item does not block a Shard or viable curse later in the received list.
  In Room_Forest_Boss_0, five earlier map effects stayed pending while one later Determination Shard
  delivered. Server confirms all six grants; zero exceptions. Curse independence is not yet observed.
- [ ] Send one Mastery item: +5 levels, once across reconnect/restart. End a run: no passive mastery reward.
  Sent one Mastery: Cetus: log confirms level 0 -> 5, +40,040 points, 1,558 points into level preserved,
  applied counter 1. In-run reconnect confirmed: still one application and one notice, no exceptions.
  Startup mastery replay remains pending. Later quit/continue logged suppression of a 59,320-point
  run reward; passive mastery suppression observed.
- [ ] Receive Stardust while Constellations is open; the UI and profile both increase by the expected pack value.

Record whether an item came from a normal check or `/send`. Exercise at least one natural item receipt as well
as controlled grants. For the exact total, sum seeded Stardust pack allocations; native run rewards and later
server-granted packs are separate income. Do not infer the total from the final currency balance.

## B. Jonas's Wares (#8 and review fixes)

- [x] A Jonas shop offers one unbought AP ware beside normal stock. Hover shows item, recipient, location and price.
  Player screenshot: Blessing: Paradox, for SoDTest; Jonas's Ware 4; 100 gold. Normal stock remains visible.
  Player reports 195 gold before purchase; expected balance after buying is 95. Purchase not yet tested.
- [x] Buying spends the displayed gold and sends exactly one ware check. It does not grant the placeholder Cloak.
  Player confirmed purchase worked as expected (195 -> 95 gold and Paradox marker). Log confirms one
  WARE_JONAS_4 check and one Paradox delivery from SoDTest, with no Cloak delivery or exceptions.
  Player reopened the shop and confirmed the purchased ware was disabled.
  UI fix (done 2026-10-05, no retest needed): the tooltip no longer shows the gold price or "Purchased".
- [ ] Skip a ware, revisit/refresh: another unbought ware may be offered. Already checked wares never return.
  Another Jonas visit offered Ware 15; player bought it, sending one check and delivering Disintegration.
  Deliberate skip/refresh and checked-stock absence after continue remain untested.
- [ ] Buy a ware offline; reconnect sends its check once. Reload/continue does not allow purchasing it again.
- [ ] Reuse the shop UI at another shop with a vanilla Treasure: the native icon returns, without an AP icon leak.
- [ ] Buy remaining wares over subsequent visits if practical; once all 30 are checked, no AP ware remains.

Literal angle-bracket/backslash tooltip encoding and server-released stale wares have automated coverage.
Mark them **automated only** unless deliberately exercised in-game; don't alter the seed/profile to fake a pass.

## C. Pilgrimage checks (#9)

- [ ] First successful use of each shrine sends its check; a failed use or later repeat sends nothing new:
  Pot of Greed, Maw of Doom, Hatred, Paradox, Mirror of Remorse, Destiny, Entanglement,
  Altar of Cleansing and Ascension.
  Pot of Greed and Hatred observed: one check each, each yielding one natural Stardust item (+675).
  Both checks survived restart and were resent on reconnect. Failed uses and repeats remain untested.
  Paradox and Destiny also sent one check each, each yielding +675 Stardust. Five total Stardust packs received
  from natural checks at that point; applied counter 25, observed balance 16,351.
  Maw of Doom subsequently sent one check and awarded Mastery: Shell (+5 levels, 0 -> 5, applied 1).
  Altar of Cleansing also sent its check and delivered a Mirror of Remorse Blessing.
  Remaining shrines: Mirror of Remorse, Entanglement and Ascension; failed uses/repeats untested.
  Souvenir Mushroom Cluster sent one check and awarded +675 Stardust; counter 26, observed balance 16,898.
  Player also completed the subsequent Challenge of Greed fight without issues.
  **Remaining shrine-use tests skipped at player request:** general shrine checking works. Do not request
  more shrine activations, failures or repeats for coverage; retain the observed checks above.
- [ ] First successful completion of each quest sends its check; failure and repetition do not:
  Stray Memory, Star Seeker's Journal, Fragment of Radiance, Call of the Ravenous,
  Consort of Night and Hunted by Obliviax (escape).
- [ ] Picking up an artifact alone sends nothing. Hand it to the Dream Teller: the artifact check sends,
  with normal journal entry and rewards. Repeat hand-in does not create a new check.
  First Artifact Blessing delivered and player picked up an artifact named Token of Servitude in-game.
  Pickup sent no artifact check (game log and server readback). Hand-in sent exactly one check for
  Artifact: Emblem of Subjugation (Artifact_EmblemOfSubjugation) and awarded +675 Stardust, counter 27.
  Token of Servitude is its observed in-game name; the location was renamed to match (2026-10-05). Native rewards
  and repeated hand-in still need observation.
- [ ] Complete a shrine/quest or hand-in offline; reconnect resends the saved check without a second reward.
  **Offline artifact hand-in skipped at player request:** second artifact is too far away; player considers
  this test noncritical. Do not pursue it or count it as passed. Offline shrine/quest coverage remains unobserved.

Track all 12 artifact names: Bouquet of Eyes, Token of Servitude, First Merchant's Certificate, Fool's Gold Coin,
Ancient Leaf Hound Egg, Nightmare Catalyst, Star Blossom, The Starlit Stone, Seeker's Tome, Whispers of the Void,
Watcher's Records and Ordinary Ring. Mark names not encountered **not observed**. A single hand-in proves the
runtime hook, not that every artifact was played. Ascension is not supplied by the Blessing pool.

## D. Map Blessings (#10 and review fixes)

Use the numbered Blessing command deck, one item at a time near the start of a world. Check delivery notice,
revealed marker, ping, route accessibility and the actual content on arrival for every type:

- [ ] Pure Dream; Gold Everywhere; Harder Fight, Better Reward.
- [ ] Blessed Guidance; Pot of Greed; Maw of Doom; Hatred; Paradox; Mirror of Remorse;
  Destiny; Entanglement; Disintegration; Altar of Cleansing.
  AP Maw of Doom destination spawned a usable shrine; player used it and one check sent. Natural Ware 15
  delivered Disintegration once; arrival/effect still needs observation.
- [ ] Lizard Shop; Artifact.
- [ ] Shrine costs/effects and room rewards stay vanilla. A Blessing itself does not send a shrine-use check.
- [x] Send two copies of the same shrine Blessing. They must not duplicate the same shrine on one node.
  Sent two Entanglement Blessings in world 4; both delivered once and player confirmed different rooms.
  Player confirmed both ping UIs worked normally. A native ping-counter OverflowException appeared in the log
  with no visible impact; player explicitly declined investigation. Do not pursue this error or shrine activation.
  Save/continue with these two shrines was not separately observed.
- [x] While carrying an artifact, send another Artifact Blessing. It waits; after hand-in, it can land.
  Second Artifact Blessing sent while player carries Token of Servitude in world 3. Server confirms two
  copies total; only one delivery notice while held. After hand-in, notice count rose to two and the player
  confirmed the new artifact marker. One artifact check total, zero exceptions.
- [ ] In an exit-boss room, a sidetrack and Primus, send a Blessing. It waits rather than spending the copy.
  On returning to a suitable normal map or starting the next run, it lands once.
  Boss-room deferral and later-world delivery verified: Maw of Doom waited in Room_Forest_Boss_0 and
  delivered once on reconnect in LavaLand; player confirmed its marker. Sidetracks and Primus remain pending.
- [ ] With no unvisited usable nodes, a Blessing waits for another world/run.

If Hunters remove a normal room bonus, record it as native behavior. Multiple Artifact destinations can have
a native route/pickup conflict; record the route rather than treating every such conflict as an AP failure.

## E. Treasures (#11 and review fixes)

- [ ] Cloak of Guidance: next **two** Hunter advances are skipped. Another copy adds its native effect.
- [ ] Clairvoyance: unvisited nodes are revealed. Another copy waits if everything is revealed, then lands
  in a later world with unrevealed nodes.
- [ ] Determination Shard: a lethal hit consumes it and restores 25% maximum health. Two Shards stack.
- [ ] Treasure Map: a quest leads to a Hidden Stash with its native reward.
- [ ] Totally Genuine Treasure Map: a quest leads to its native stash/bonus/ambush result.
  No need to force all random outcomes.
- [ ] Send two maps together. Both can initialize; their quests/markers remain usable across travel and continue.
  Repeat just after a native map purchase to cover the spawn-initialization race.
  Treasure Map and Totally Genuine Treasure Map were queued together in world 1's boss room; each delivered
  once in world 2 and both native quests started, without exceptions. Player confirmed both quests/markers
  and Clairvoyance's revealed map. Subsequent travel/continue with both quests and the native-purchase race
  remain pending.
  Both world-2 map quests subsequently ended their Default steps during room travel; zero exceptions.
  Specific rewards/outcomes were not confirmed by the player. Both-active save/continue remains untested.
- [x] In an exit-boss room, Cloak, Clairvoyance and maps wait; a Shard still lands.
  Sent one each of Cloak, Clairvoyance, Treasure Map and Totally Genuine Treasure Map, then one Shard
  (after a Maw of Doom Blessing). Only Shard delivered; zero exceptions. All six server grants verified.
- [ ] Maps wait in sidetracks/Primus and when no suitable node remains, then land in another world/run.
- [x] Shard + received DeathLink: the Shard saves you; no reflected DeathLink is sent.
  Sent one controlled DeathLink with two Shards believed active and an execution curse at <=40% HP.
  Player confirmed one Shard consumed and survival. Execution curse did not trigger because this was not
  enemy damage. Listener observed no outgoing/reflected DeathLink, and no knockout or exceptions logged.
- [ ] The next normal knockdown sends a DeathLink after the absorbed incoming one.
  **Skipped at player request:** outgoing DeathLink is existing functionality. No deliberate knockdown needed.
- [ ] Without a Shard, a received DeathLink kills you once, without echo.

Use `tools/deathlink_test.py` on the same slot for that last check. DeathLink can end a solo run, so test it
after the map and shrine observations. The earlier bleeding-out suppression issue remains a separate known
follow-up; distinguish it from the Shard regression if encountered.

## F. Curse traps (#12)

First controlled trap: one Curse: Mild delivered in Room_Sky_Combat_4; native Quest_KillToLiftCurse started.
Player confirmed Unstable Power I with 32 kills required, matching 28 + world-index 2 × 2.
Player confirmed it disappeared after reaching the kill count. Potent delivered Fragile II with four new areas
required, matching the travel rule. The native Quest_KillToLiftCurse class handles both kills and travel;
its log name alone does not identify the lift condition. Intense delivered Amplified Pain III with 56 kills,
matching 50 + world-index 2 × 3. One delivery per tier and zero exceptions in the log.

- [x] Send Mild, Potent and Intense separately. Each creates a vanilla curse on your Traveler, with the correct
  tier, native notification and quest tracker. Record the randomly selected curse.
- [ ] Kill/travel progress advances and removes the curse when the tracker completes. Observe both lift modes
  if possible; mark any unobserved mode accordingly.
- [x] Save/continue: active curse effects/progress persist without replay.
  Player confirmed restoration; both active curse quests loaded from save. No new curse delivery notices
  after the latest Continue, and no exceptions. Earlier notices in the same log belong to pre-save grants.
- [ ] Receive duplicate curses and reconnect: no extra replay; native duplicate behavior remains intact.
- [ ] Send a trap in an exit-boss room, Primus boss and Polaris fight if reached. It waits, then applies in a
  suitable later room/run. A knockdown removes curses as in vanilla.
  Sent one additional Mild curse in Room_Sky_Boss_0. Server confirms two Mild copies total (58 received items);
  delivery notices remained at one in the boss room, then rose to two on arrival in Zone_Despair/world 4.
  Player confirmed activation; one new quest started, zero exceptions. Exit-boss deferral/later-world delivery
  passed; Primus/Polaris and knockdown removal remain unobserved.
- [ ] A pending map Blessing or Treasure does not prevent a viable trap from applying.

Keep the full vanilla curse pool. No exclusions for Dark Urge, Intermittent Explosion or Inductive Dream
Affliction. This solo test does not verify their effects on guests or exhaust all 23 random curse prefabs.

## G. Finish and report

- [ ] End the test without completing the goal. Goal behavior is not part of this plan.
- [ ] Server and game logs contain no new exceptions, unknown item IDs, missing targets or data mismatch.
- [ ] Record pass/fail/not observed for each checkbox, with item, room/world, delivery timing and log evidence.
  Save `Player.log` before restarting the game (the next session replaces it).

Do not call the full plan passed while RNG-dependent checks or guard scenarios remain unobserved. Record them
separately from automated coverage. Preparation did not create or modify game profiles; the player subsequently
created and bound **AP 7-12** to start the test.
