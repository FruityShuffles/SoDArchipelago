# Shape of Dreams

## What does randomization do to this game?

In vanilla Shape of Dreams, every achievement unlocks one thing: a Traveler, a Memory, an Essence or a Lucid Dream.
In Archipelago those 93 unlocks are shuffled into the multiworld as items, and achievements and world clears become
the checks that send items to you and to the other players.

Your normal save is never touched. Each seed is played on its own game profile, which the client mod binds to your
slot the first time you connect.

## Where are the checks (locations)?

There are 228 locations.

- **Achievements (93):** completing any achievement sends its check. You still get the achievement's usual Stardust
  bonus, but not its usual unlock. That unlock is an item somewhere in the multiworld.
- **World clears (135):** clearing world 1 to 5 as each of the 9 Travelers, on Deep Sleep, Ominous Dream and
  Nightmare. A world counts as cleared when you move on from it (to the next world, or into the ending after world 5).
  Clearing a world on a harder difficulty also sends that world's checks for the easier difficulties. Nap has no
  checks, and loops past world 5 send nothing.

World clears are **priority locations**, so they tend to hold progression items. Achievements can hold anything.

Location groups you can use in `exclude_locations` / `priority_locations`: `Achievements`, `World Clears`,
`Build-Dependent Achievements` (achievements that need specific Memories in your build), one group per Traveler
(e.g. `Aurena`: that Traveler's achievements and world clears) and one per difficulty (`Deep Sleep`,
`Ominous Dream`, `Nightmare`).

## What items can I receive?

- **Progressive Traveler** (progression). For Aurena, Bismuth, Cetus, Nachia, Shell, Vesper and Yubar, the first copy
  unlocks the Traveler and the next three unlock their three alternate Memories (Q, then R, then Identity). Lacerta and
  Mist start unlocked, so each of their three copies is an alternate Memory.
- **Memory / Essence / Lucid Dream unlocks** (useful): the other 59 achievement unlocks. A Memory or Essence can drop in
  runs once you have it. A Lucid Dream can be selected in the lobby.
- **Mastery: \<Traveler\>** (filler): +5 mastery levels for that Traveler.
- **Stardust** (filler): +650 Stardust.

With default settings the filler alone gives every Traveler 35 mastery (the most any star or star slot needs) and
enough Stardust to buy every star in the constellation, with some left over for star slots. There are no traps.

Unlocks take effect the way they do in vanilla. Everything is saved to your profile straight away, but the loot pool
is built when a run starts and Travelers, loadouts and Lucid Dreams are picked in the lobby. An item that can't change
the run you're in is marked "(next run)".

## Which Travelers and difficulties do I need? (logic)

- An achievement that has to be done as a specific Traveler needs that Traveler.
- A Deep Sleep world clear needs that Traveler.
- An Ominous Dream world clear needs the Traveler plus one alternate Memory (Lacerta and Mist: one copy).
- A Nightmare world clear needs the Traveler plus two alternate Memories (Lacerta and Mist: two copies).

## What is the goal?

Win a run at `goal_difficulty` or harder with `goal_traveler_count` different Travelers. A win is reaching either
ending, the Pure White Dream or the Starless Path. Logic only requires you to have enough Travelers unlocked; it never
requires their Memories for the goal.

## Options

| Option | Default | Range | What it does |
|---|---|---|---|
| `goal_difficulty` | `nightmare` | `deep_sleep`, `ominous_dream`, `nightmare` | Lowest difficulty a win counts at |
| `goal_traveler_count` | 9 | 1–9 | Different Travelers that must win |
| `mastery_packs_per_traveler` | 7 | 0–15 | Mastery items per Traveler. The rest of the filler is Stardust |
| `mastery_pack_value` | 5 | 1–35 | Mastery levels per Mastery item |
| `stardust_pack_value` | 650 | 1–10,000 | Stardust per Stardust item |
| `death_link` | off | | When you are knocked down, everyone on DeathLink dies, and the other way round. In co-op, only the host receives DeathLinks (see Co-op) |

## Playing offline

You can play a bound profile without being connected, using the items you have already received. Checks are recorded
in the profile and sent when you reconnect, and the mod shows an "OFFLINE" notice the whole time. Other players won't
get the items from those checks until you reconnect.

## Co-op

Each player uses their own profile, mod and slot. The loot pool of a co-op run is the union of every player's unlocks,
as in vanilla.

**DeathLink in co-op:** only the host of a co-op run can be killed by a DeathLink. The game only lets the host kill a
Traveler, so if you join someone else's game, DeathLinks sent to you do nothing. The mod shows the DeathLink with
"can't be applied". You still send DeathLinks when you are knocked down. Solo players and the host are not affected.
