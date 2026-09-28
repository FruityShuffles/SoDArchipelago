# Shape of Dreams Setup Guide

## Required software

- Shape of Dreams (Steam)
- The SoDArchipelago client mod (the `SoDArchipelago` folder)
- [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases) 0.6.4 or newer, to generate and host games,
  with `shape_of_dreams.apworld` installed (double-click it, or copy it into Archipelago's `custom_worlds` folder).
  Download it from the [SoDArchipelago releases](https://github.com/FruityShuffles/SoDArchipelago/releases).

## Installing the mod

1. Copy the `SoDArchipelago` folder into `<Shape of Dreams install>/Mods/`.
2. Start the game and enable **Archipelago** in the in-game mod manager.

The mod marks your lobbies as modded, because it changes what you unlock.

## One profile per seed

Every Archipelago seed (and slot) is played on its own game profile. The mod never reads or changes a profile that
isn't bound to the slot you're connected to, so your normal save is safe.

1. On the title screen, open the profile selection and **create a new profile** for this seed. Don't play a run on it
   yet.
2. Set your connection details. Either use the mod's config in the mod manager (server, slot and password), or open the
   console and type:
   - `ap_server archipelago.gg:38281` (the address and port of the room)
   - `ap_slot YourSlotName`
   - `ap_password yourpassword` (only if the room has one)
3. On the title screen, type `ap_connect` in the console. The first time, the mod asks you to bind the new profile:
   type `ap_bind`. The mod logs in first and only binds the profile if that works (so a mistyped slot name or password
   binds nothing). The profile is then bound to that seed and slot for good. Binding only works on the title screen,
   not in a lobby or a run.
4. Play. Next time, load the same profile and type `ap_connect`. That's all.

If you connect with the wrong profile loaded, the mod refuses and tells you which seed and slot the profile belongs to.
It also refuses a seed generated with a different version of the game data than the mod has (for example after a
game update): use the apworld and the mod from the same release.
It only binds a profile that has no completed achievements and no runs yet. If you really need to bind a profile that
isn't fresh (for example, to recover a seed after deleting its profile), `ap_bind_force` skips that check. It still
never takes over a profile that is bound to a different seed.

The room's port can change (for example when an archipelago.gg room restarts). Just update the server address; the
binding doesn't depend on it.

Steam achievements aren't updated while a bound profile is loaded, so a fresh Archipelago profile can't lower your
Steam achievement progress.

## Commands

| Command | What it does |
|---|---|
| `ap_connect` | Connect to the server, slot and password you set |
| `ap_bind` | Bind the loaded (new) profile to the seed and slot you just connected to |
| `ap_bind_force` | Same, but skip the "fresh profile" check (recovery only) |
| `ap_disconnect` | Disconnect |
| `ap_status` | Show the connection and profile status |
| `ap_server <address>`, `ap_slot <name>`, `ap_password <password>` | Change the connection settings |

## Playing offline

A bound profile keeps working when you're not connected: you keep what you've already received, completed achievements
and world clears are saved in the profile, and everything is sent the next time you connect. An
"OFFLINE — checks will send on reconnect" notice stays on screen while you're disconnected.

Other players don't get the items from your checks until you reconnect, so connect when you can.

## DeathLink

If you turn on `death_link`, being knocked down sends a DeathLink to everyone else on DeathLink, and a DeathLink from
someone else kills your Traveler. In solo that ends the run; in co-op it's a normal knockdown your teammates can revive.

**Limitation: if you join someone else's co-op game, DeathLinks can't kill you.** The game only lets the host kill a
Traveler, so the mod can't apply a DeathLink to a joining player. It shows the DeathLink on screen with
"(can't be applied: only the host can kill Travelers)" and your Traveler is unaffected. Your own knockdowns still send
DeathLinks as usual. This only affects joining players; solo players and the co-op host receive DeathLinks normally.

## Troubleshooting

The mod logs everything with an `[AP]` prefix to
`%USERPROFILE%\AppData\LocalLow\Lizard Smoothie\Shape of Dreams\Player.log`. Include that file when you report a bug.
