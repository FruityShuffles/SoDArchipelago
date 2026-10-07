# Solo test results: issues #7–#12

**Completed 2026-10-06.** Testing ran from 2026-10-04 through 2026-10-06. The agreed core tests
passed. Startup passive mastery is an accepted limitation ([issue #13](https://github.com/FruityShuffles/SoDArchipelago/issues/13)).
Skipped and unobserved cases below are not passes. No co-op or goal-completion testing was performed.

Scope: issue commits `f06dbef`, `923137d`, `ae009a8`, `251effd`, `e33f4e1`, `7e0e1d9`,
review/classification fixes `56237e8` and `6143faa`, performance fix `3e8a052`, and follow-up fixes `095f0cc`.

## Environment

| Setting | Tested value |
|---|---|
| Game / Archipelago | `r.1.4.0.13_s` / 0.6.8 |
| Seed | `44140295405363709462`, generation seed `20261004` |
| YAML | [solo-issues-7-12.yaml](solo-issues-7-12.yaml) |
| Connection | `localhost:38281`, slot `SoDTest`, blank password |
| Bound profile | `AP 7-12`, created and bound by the player |
| Run | Dream Alone, Deep Sleep, Cetus; starting Travelers Cetus and Shell |
| Data | Format 5; hash `fbb7bcce1eae8c6c28e8e5897c08f46864789070b29a034dca88783c6f0842c0` |

In-run items, traps, 30 Jonas wares, shuffled star requirements and DeathLink were enabled.
Passive mastery was disabled; no forced Lucid Dreams. The Nightmare/all-nine-Traveler goal was
not completed, and normal collection settings were unchanged. Mod metadata was 0.5.0 and
apworld version 0.5.1; the deployed build was updated during testing.

## Automated preparation

- Build/deployment passed with zero warnings/errors. Preparation ran 580 AP/general tests and
  682 C# assertions; the performance fix passed 686 C# assertions.
- All 12 pool scenarios passed: wares 0/30/100 with every in-run-item/trap toggle combination.
- Seed: 302 locations (93 achievements, 135 world clears, 17 souvenirs, 30 wares, 9 shrines,
  6 quests, 12 artifacts). Deep Sleep clears were priority; souvenirs and artifacts excluded.
- Pool: 34 progression, 131 useful, 126 filler, 11 traps. All 72 Mastery items were useful.
- Seeded Stardust: 80 packs totalling 54,001 (first 676, remaining 675 each). Extra server
  grants and native rewards were separate income.
- Fresh binding and compatibility passed. No unknown item IDs or data mismatch observed.
- Tooltip escaping and server-released stale wares had automated coverage only.

These checks do not establish in-game coverage of every item or location.

## Passed in-game tests

| Area | Result and evidence |
|---|---|
| Connected performance | Player confirmed smooth frame pacing after `3e8a052` removed idle delivery-loop scene searches. |
| Lobby queue and restart | Pure Dream and Cloak remained pending across a full restart in the lobby, then each delivered once in the run. |
| Reconnect and repeat copies | Reconnect did not replay delivered items. A second Pure Dream produced a second marker. |
| Offline receipt | A Treasure Map granted while disconnected stayed undelivered through offline travel, then landed once on reconnect. |
| Save/Continue | Delivered effects/counters survived. A native Treasure Map quest restored without another item delivery. |
| Boss and later-world deferral | Maw, Cloak, Clairvoyance and both map types waited in the Forest boss room, stayed pending through offline travel, then delivered once in world 2. A later Shard delivered in the boss room despite them. |
| Mastery item replay protection | Cetus gained five levels, preserving 1,558 points into the level. Reconnect and full restart/reconnect produced no repeated Mastery applications. Shell, Mist and Vesper retained level 5. Startup run mastery is a separate finding below. |
| Passive mastery after mod load | Quit/Continue logged suppression of a 59,320-point run reward. |
| Stardust with Constellations open | One extra pack increased the open UI from 18,454 to 19,129. Closing/reopening retained 19,129; log confirmed one +675 application. |
| Queue independence | First of two new Clairvoyance copies revealed the map; second stayed pending. A subsequent Mild curse delivered and appeared on the Traveler. |
| Online Jonas purchase | Ware 4 cost 100 gold, reduced 195 to 95, sent one check and delivered Paradox without the placeholder Cloak. It remained disabled. A later shop offered Ware 15 and delivered Disintegration. |
| Offline Jonas purchase | Ware 9 showed “an unknown ware” offline and recorded its check locally, absent from the server before reconnect. Price was 254 gold, with 1,859 before; exact post-purchase gold was not separately reported. Reconnect sent the check, awarded Stardust once (+675) and revealed its name. After quit/Continue it remained unbuyable. |
| Shrine check hook | Pot of Greed, Hatred, Paradox, Destiny, Maw of Doom and Altar of Cleansing each sent their check. |
| Quest completion hook | Stray Memory completed in `Room_Despair_Combat_1_3`. Log recorded one `Quest_StrayMemory` check; server confirmed the location. |
| Artifact hand-in hook | Token of Servitude pickup sent nothing. Hand-in sent one `Artifact_EmblemOfSubjugation` check and awarded Stardust. |
| Artifact Blessing deferral | Second Artifact Blessing waited while an artifact was held, then placed a marked destination after hand-in. |
| Duplicate shrine placement | Two Entanglement Blessings landed in different rooms. Both ping UIs worked normally. |
| Both map types together | Treasure Map and Totally Genuine Treasure Map initialized in world 2; player confirmed both quests/markers and Clairvoyance's revealed map. |
| Shard with incoming DeathLink | One controlled DeathLink consumed one Shard; player survived. No reflected DeathLink or knockout observed. The execution curse did not trigger because there was no enemy damage. |
| Curse tiers | Mild: Unstable Power I, 32 kills; Potent: Fragile II, four new areas; Intense: Amplified Pain III, 56 kills. Notifications/trackers matched requirements. Unstable Power lifted after the kills. |
| Curse persistence | Active effects/progress survived Save/Continue. Native quests restored without new delivery notices. |
| Curse boss deferral | Mild waited in the world-3 boss room and delivered once on entering world 4. |

Natural achievement, world-clear and souvenir checks also awarded items. At the Stray Memory
readback, the server held 69 received items and 23 checked locations.

## Findings and completed fixes

- **Connected stutter fixed:** `3e8a052`; player confirmed the improvement.
- **Jonas tooltip fixed:** `095f0cc` removed redundant price and “Purchased” text. No additional
  UI retest was required. This is not outstanding.
- **Artifact names fixed:** `095f0cc` aligned English names, including Token of Servitude,
  preserving keys, IDs and data hash. Earlier logs use “Emblem of Subjugation.”
- **Startup passive mastery:** after closing mid-run, the player saw the normal mastery popup
  on the next title screen, increasing Cetus to level 10 despite passive mastery being off.
  Startup diagnostics already showed level 10 with 15,640 points before connection; no AP Mastery
  item reapplied. Cause: the title screen rewards the unfinished run before mods load. Accepted as a
  known limitation ([issue #13](https://github.com/FruityShuffles/SoDArchipelago/issues/13)). After the run
  was continued and finished, Cetus was back at level 5: the game takes back a conceded run's mastery.
- A native ping-counter `OverflowException` followed the Entanglement grants. Both ping UIs
  worked normally; the player explicitly declined investigation. No further work is planned on it.
- After an abrupt server stop, the online indicator remained stale until manual disconnect/reconnect.
  Automatic reconnect is outside this test, tracked separately in issue #3.

## Skipped at the player's request

- Remaining shrine activations, failed uses and repeats: the general check hook worked.
- Offline artifact hand-in: the second destination was too far away; considered noncritical.
- Outgoing DeathLink after a normal knockout: existing functionality.
- Investigation of the native ping-counter exception: no visible impact.

## Not observed

These optional cases were left unobserved when the core test concluded. They are not pending
instructions to continue this session.

- All six quests individually, quest failure/repetition, and offline shrine/quest completion.
- All 12 artifact hand-ins, repeated hand-in, and separate confirmation of native journal/rewards.
- Every Blessing's destination/effect, shrine cost comparison, and Save/Continue with both Entanglement shrines.
- Map guards in sidetracks/Primus, exhausted usable-node maps, and the pending second Clairvoyance's
  delivery in a later world/run.
- Cloak's exact two-Hunter-advance duration, enemy-hit Shard stacking, map reward outcomes,
  both map quests active through Save/Continue, and the native-purchase initialization race.
- Jonas deliberate skip/refresh, vanilla icon restoration in reused shop UI, and exhausting all 30 wares.
- Curse travel-mode lifting to completion, duplicate native curse behavior, Primus/Polaris deferral
  and knockout removal. The full vanilla curse pool remains intact; no guest-effect coverage is claimed.
- Incoming DeathLink without a Shard. No goal or co-op coverage.

## Evidence

Ignored session records and log archives are under `.claude/solo-issues-7-12/`, unavailable from
a fresh clone. Archives 01–33 cover initial tests; 34–35 full restart/reconnect mastery; 36 Stardust
with Constellations open; 37 queue independence; 38–40 offline Ware 9 purchase/reconnect/Continue;
41 Stray Memory completion. Historical notices in full logs are not, by themselves, replay evidence.
