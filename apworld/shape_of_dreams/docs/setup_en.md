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

## One profile per seed

Every Archipelago seed (and slot) is played on its own game profile. The mod never reads or changes a profile that
isn't bound to the slot you're connected to, so your normal save profile will be untouched.

1. On the title screen, open the profile selection and **create a new profile** for this seed. Don't play a run on it
   yet.
2. Set your connection details. Open up the mod's config in the mod manager (click the gear icon after selecting the mod).
   Fill out the following fields there:
   - Server: archipelago.gg:38281 (use the address and port of your room, this is for example)
   - Slot: YourSlotName (the name of your Archipelago slot, not the name of your in-game profile)
   - Password: roompassword (only if the Archipelago room has one)
3. Enable Developer Mode in the in-game Gameplay settings. This will allow you to use the F1 key to open up the console.
   The first time you press F1, you may need to click the square icon that appears on the right side of the screen to open the console.
4. On the title screen (not the lobby), type `ap_connect` in the console. The first time, the mod asks you to bind your selected game profile:
   type `ap_bind`. The profile is then bound to that seed and slot for good.
5. Play. If you restart the game, load the same profile and type `ap_connect` to reconnect to the archipelago server.

Steam achievements aren't updated while you are playing on an Archipelago bound profile.

## Commands

| Command | What it does |
|---|---|
| `ap_connect` | Connect to the server, slot and password you set |
| `ap_bind` | Bind the loaded (new) profile to the seed and slot you just connected to |
| `ap_bind_force` | Same, but skip the "fresh profile" check (recovery only) |
| `ap_disconnect` | Disconnect |
| `ap_status` | Show the connection and profile status |
| `ap_server <address>`, `ap_slot <name>`, `ap_password <password>` | Change the connection settings (best to do this in the mod config instead) |

## Playing offline

A bound profile keeps working when you're not connected: you keep what you've already received, completed achievements
and world clears are saved in the profile, and everything is sent the next time you connect. An
"OFFLINE — checks will send on reconnect" notice stays on screen while you're disconnected.

## Co-op
If you disable crossplay, you may play co-op with other players regardless of whether they are also playing Archipelago or not.
Be aware that a player who joins someone else's lobby can't receive DeathLinks. Two archipelago players playing together won't
be able to receive DeathLinks for the joining player.

## Troubleshooting

The mod logs everything with an `[AP]` prefix to
`%USERPROFILE%\AppData\LocalLow\Lizard Smoothie\Shape of Dreams\Player.log`. Report bugs on
[GitHub Issues](https://github.com/FruityShuffles/SoDArchipelago/issues) and attach that file.
