# Shape of Dreams Archipelago

An [Archipelago](https://archipelago.gg/) randomizer for [Shape of Dreams](https://store.steampowered.com/app/2444750/).
Archipelago is a multi-game randomizer: players in different games share one shuffled item pool, so what you find can
be someone else's item, and theirs can be yours.

In this randomizer, achievements, world clears and lizard shop souvenirs are the checks, and the Traveler, Memory,
Essence and Lucid Dream unlocks (plus Mastery and Stardust) are the items. You start with 2 random Travelers instead of
Lacerta and Mist. Each seed is played on its own game profile, so your normal save is never touched.

Please [report bugs](https://github.com/FruityShuffles/SoDArchipelago/issues).

## Options

Set these in your YAML. The [example YAML](examples/Shape%20of%20Dreams.yaml) explains every one.

- **Goal:** the lowest difficulty a win counts at, and how many different Travelers must win (1–9).
- **Forced Lucid Dreams:** pick Evil or Chaotic Lucid Dreams to be forced on in every run until you receive their item.
- **No passive mastery:** runs earn no mastery, so it only comes from Mastery items (off by default).
- **Shuffled constellation stars:** the mastery level each star needs is shuffled (on by default).
- **DeathLink:** when one player is knocked down, everyone on DeathLink goes down.
- **Pool and checks:** how much Mastery and Stardust the filler gives, and which checks (by Traveler, difficulty or
  achievement type) are important or excluded.

The [game info page](apworld/shape_of_dreams/docs/en_Shape%20of%20Dreams.md) has the details.

## Getting started

1. Subscribe to the [Archipelago Randomizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3809735647) mod on
   the Steam Workshop.
2. Download `shape_of_dreams.apworld` from the [release matching your mod version](https://github.com/FruityShuffles/SoDArchipelago/releases)
   and install it in [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases) 0.6.4 or newer.
3. Start from the [example YAML](examples/Shape%20of%20Dreams.yaml): the recommended settings, with every option explained.
4. Follow the [setup guide](apworld/shape_of_dreams/docs/setup_en.md) to connect.

The [game info page](apworld/shape_of_dreams/docs/en_Shape%20of%20Dreams.md) explains the checks, items, goal and
options.

## Contributing

To build the mod or the apworld, or to see how the randomizer works, read [CONTRIBUTING.md](CONTRIBUTING.md).

## Credits

- [Shape of Dreams](https://store.steampowered.com/app/2444750/) and its official mod loader are by Lizard Smoothie.
- [Archipelago](https://archipelago.gg/) is built by the Archipelago community.
- The mod talks to Archipelago through [Archipelago.MultiClient.Net](https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net) (MIT).

## License

[MIT](LICENSE). Not affiliated with Lizard Smoothie. Shape of Dreams is their game; this repository contains none of it.
