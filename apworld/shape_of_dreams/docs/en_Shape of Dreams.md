# Shape of Dreams

## What does randomization do to this game?

In vanilla Shape of Dreams, the in-game achievement system is used to unlock Travelers, Memories, Essences and Lucid Dream run modifiers.
In Archipelago those 93 unlocks are shuffled into the multiworld as items, and achievements, world clears and lizard
shop souvenirs become the checks that send items to you and to the other players. Lacerta and Mist are items too: instead of them, you start
with 2 random Travelers.

Your normal save is never touched. Each seed is played on its own game profile, which the client mod binds to your
slot the first time you connect.

## Where are the checks (locations)?

There are 245 locations.

- **Achievements (93):** completing any achievement sends its check. You do not get the usual unlock for the achievement.
  That unlock is an item somewhere in the multiworld.
- **World clears (135):** clearing worlds 1 to 5 as each of the 9 Travelers, on Deep Sleep, Ominous Dream and
  Nightmare. Winning through any ending clears all worlds. Clearing a world on a harder difficulty also sends
  that world's checks for the easier difficulties. Nap has no checks, and the worlds of later loops send nothing.
- **Souvenirs (17):** owning each souvenir the lizard shop sells (bought, or unlocked with a code) sends its check. You
  keep the souvenir.

Deep Sleep world clears are **priority locations**, so they tend to hold progression items. Achievements can hold anything.
Souvenirs are always excluded (the shop offers them at random), so they only hold filler.

### Customizing your checks

You can customize which checks may hold important items using these location groups:

- `exclude_locations`: those checks only get filler (Stardust, Mastery, or another game's filler), so you never have to
  do them to progress.
- `priority_locations`: those checks get progression items. Deep Sleep clears are already priority; excluding one overrides that.

| If you want to… | Put this in your YAML |
|---|---|
| Play on an easier difficulty without progression getting stuck behind Nightmare runs | `exclude_locations: [Nightmare]` |
| Not get stuck waiting for a lucky build | `exclude_locations: [Build-Dependent Achievements]`: the 9 achievements that need specific Memories in your build. |
| Focus on winning runs and not chase in-game achievements | `exclude_locations: [Achievements]` |
| Skip a Traveler you don't enjoy | `exclude_locations: [Aurena]` (that Traveler's achievements and world clears), with `goal_traveler_count` below 9 |
| Get your progression from a Traveler you like | `priority_locations: [Aurena]` |

Groups: `Achievements`, `World Clears`, `Build-Dependent Achievements`, `Souvenirs`, one per Traveler (`Aurena`, `Bismuth`, `Cetus`,
`Lacerta`, `Mist`, `Nachia`, `Shell`, `Vesper`, `Yubar`) and one per difficulty (`Deep Sleep`, `Ominous Dream`,
`Nightmare`).

You should leave at least 93 checks un-excluded (checks that are already excluded, like the souvenirs, don't count). The 93 unlock items in your pool can't go on excluded checks, so
excluding more makes generation fail.

## What items can I receive?

- **Progressive Traveler** (progression). The first copy unlocks the Traveler and the next three unlock their three alternate Memories (Q, then R, then Identity).
  Your 2 starting Travelers' first copies are starting items (`(starting item)` in the message feed).
- **Memory / Essence / Lucid Dream unlocks** (useful). Unlocked Memories or Essences can drop in runs. Unlocked Lucid Dream run modifiers
   can be selected in the lobby.
- **Mastery: \<Traveler\>** (filler): +5 mastery levels for that Traveler.
- **Stardust** (filler): a share of `stardust_total`. Packs divide the total evenly, with one extra Stardust in
  the first few packs when needed. Changing the number of packs keeps total income the same.

With default settings the filler alone gives every Traveler 40 mastery and enough Stardust to buy every constellation star, star slot and souvenir.

## What do I see when I send or receive an item?

A message feed in the top-left corner of the screen shows `Received <item> from <player>` and the items you send to
other players. If you receive an unlock or Mastery during a run, its message ends with `(next run)`: it applies from
your next run.

## What is the goal?

Win a run at `goal_difficulty` or harder with `goal_traveler_count` different Travelers. A win is reaching either
ending, the Pure White Dream or the Starless Path.

## Options

| Option | Default | Range | What it does |
|---|---|---|---|
| `goal_difficulty` | `nightmare` | `deep_sleep`, `ominous_dream`, `nightmare` | Lowest difficulty a win counts at |
| `goal_traveler_count` | 9 | 1–9 | Different Travelers that must win |
| `mastery_packs_per_traveler` | 8 | 0–15 | Mastery items per Traveler. Remaining baseline filler slots stay Stardust |
| `mastery_pack_value` | 5 | 1–40 | Mastery levels per Mastery item |
| `stardust_total` | 54,000 | 0–1,000,000 | Total Stardust from the seed, divided exactly across its packs |
| `in_run_items` | off | | Fill new slots with Map Blessings and Treasures; solo/host delivery |
| `traps` | off | | Fill 20% of new slots with Curse traps; solo/host delivery |
| `jonas_wares` | 30 | 0–100 | Jonas's Archipelago wares; set to 0 if you mostly join other people's games |
| `passive_mastery` | on | | Off: runs earn no mastery, so it only comes from Mastery items, and "The Road Not Taken" is excluded |
| `death_link` | off | | When you are knocked down, everyone on DeathLink dies, and the other way round. |
| `forced_evil_lucid_dreams` | none | see below | Evil Lucid Dreams forced on in every run until you receive their Lucid Dream item |
| `forced_chaotic_lucid_dreams` | none | see below | The same for Chaotic Lucid Dreams |
| `shuffle_star_requirements` | on | | Shuffles the mastery level each constellation star needs, among stars of the same Traveler (or the common stars) and category |

### Forced Lucid Dreams

The listed dreams stay on in every run until you receive their `Lucid Dream: <name>` item. After that you can turn
them on or off as usual. A forced dream's item is progression. Only the lobby host's forced dreams are turned on automatically.
World clears and wins only count in a run where every still-forced dream is on.

- Evil: `Fish Scales`, `Grievous Wounds`, `Mad Life`, `Marsh of Destiny`, `Overpopulation`, `Prudent Jellyfish`
- Chaotic: `Embrace Mortality`, `Harmless Whispers`, `Sparkling Dream Flask`, `The Darkest Urge`, `WILD`

```yaml
forced_evil_lucid_dreams: ["Grievous Wounds", "Overpopulation"]
forced_chaotic_lucid_dreams: ["WILD"]
```

### In-run presets

The [example YAML](https://github.com/FruityShuffles/SoDArchipelago/blob/main/examples/Shape%20of%20Dreams.yaml)
has three presets: Default (`in_run_items: false`, `traps: false`), With in-run items (`in_run_items: true`),
and With in-run items and traps (both `true`). The default preserves the base game's run balance.

In-run items wait until you are connected and playing solo or hosting, and their vanilla effect has a valid target.
They can wait until a later world or run; reconnecting keeps pending items. Their message appears when they land.
Quitting before the game's next continue save can lose an effect that has already landed.

**Development status:** the shared options and pool rules are implemented in issue #7. The new locations and effects
arrive in #8–#12; all six issues ship together. Until then there are still 245 locations and no new in-run effects.

## Playing offline

You can play a bound profile offline using the items you have already received. Checks are recorded in the profile and sent when you reconnect.

## Co-op

Non-crossplay co-op is supported with other players regardless of whether they are also playing Archipelago.
A player who joins someone else's lobby can't receive DeathLinks, buy Archipelago wares or receive in-run effects.
In-run items wait until that player plays solo or hosts.
