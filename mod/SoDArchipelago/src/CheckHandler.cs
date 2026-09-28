using System;
using System.Collections.Generic;
using System.Linq;

namespace SoDArchipelago
{
    // Location checks (DESIGN.md "Checks"). Checks are recorded in the bound profile (achievements by the game itself,
    // world clears by ApRecords) and sent when connected. On every connect the full list is rebuilt from the profile
    // and resent; the server ignores duplicates.
    public static class CheckHandler
    {
        // AchievementManager.CompleteAchievement patch, after the game has recorded the completion.
        public static void OnAchievementCompleted(string achievementKey)
        {
            if (!ProfileGuard.Marked) return;
            if (!GameData.LocationsByKey.TryGetValue(achievementKey, out var loc))
            {
                Log.Warn($"Achievement {achievementKey} completed but isn't a location");
                return;
            }
            Log.Info($"Check: {loc.Name} ({achievementKey}){(ProfileGuard.Bound ? "" : " - offline, sent on reconnect")}");
            ApClient.SendLocations(new[] { loc.Id });
        }

        // World clear, worlds 1-4 = the Traveler moves on from world N: to world N+1, or after world 4 into Zone_Primus
        // (world 5) or the next loop. ZoneManager.currentZoneIndex starts at -1, is 0 in world 1 and keeps counting
        // through loops, so on arrival it equals the number of the world just left. It is a SyncVar set long before this
        // RPC fires (the room load happens in between), so it is current on joining clients too. World 5 (Primus) has no
        // zone change after it; GoalHandler clears it on a Pure White Dream win.
        public static void OnZoneLoaded(EventInfoLoadZone info)
        {
            var zm = NetworkedManagerBase<ZoneManager>.instance;
            var gsm = NetworkedManagerBase<GameSettingsManager>.instance;
            var hero = DewPlayer.local != null && DewPlayer.local.hero != null ? DewPlayer.local.hero.GetType().Name : null;
            string difficulty = gsm != null ? gsm.difficulty : null;
            int zoneIndex = zm != null ? zm.currentZoneIndex : -99;
            Log.Info($"Zone loaded: from='{info.from}' to='{info.to}' traveling={info.isTraveling} " +
                     $"fromSave={info.isLoadingFromSave} zoneIndex={zoneIndex} loop={(zm != null ? zm.loopIndex : -1)} " +
                     $"difficulty={difficulty} ({Diagnostics.DifficultyName(difficulty)}) hero={hero}");

            if (!ProfileGuard.Marked) return;
            if (!info.isTraveling || info.isLoadingFromSave || string.IsNullOrEmpty(info.from)) return;
            int world = zoneIndex;
            if (world < 1 || world > GameData.NormalWorlds)
            {
                Log.Info($"No world clear: left world {world} (only worlds 1-{GameData.NormalWorlds} of the first " +
                         "loop count here; later loops send nothing).");
                return;
            }
            RecordClears(new[] { world }, difficulty, hero, "moved on");
        }

        // Records and sends the clears of the given worlds for this Traveler, at this difficulty and every lower one
        // (DESIGN.md "Cumulative"). Used for zone changes and for the endings (GoalHandler). Marked profiles only.
        public static void RecordClears(IReadOnlyCollection<int> worlds, string difficulty, string hero, string why)
        {
            if (!ProfileGuard.Marked) return;
            int rank = GameData.RankOfGameDifficulty(difficulty);
            if (hero == null || !GameData.Travelers.ContainsKey(hero))
            {
                Log.Warn($"World(s) {string.Join(",", worlds)} cleared ({why}), but the local Traveler '{hero}' is unknown.");
                return;
            }

            var keys = worlds
                .SelectMany(w => GameData.Difficulties
                    .Where(d => d.HasLocations && d.Rank <= rank)
                    .Select(d => GameData.WorldClearKey(w, d.Key, hero)))
                .ToList();
            if (keys.Count == 0)
            {
                Log.Info($"World(s) {string.Join(",", worlds)} cleared on '{difficulty}' ({why}), which has no checks.");
                return;
            }

            bool added = false;
            foreach (var key in keys) added |= ApRecords.AddClear(key);
            if (added) DewSave.SaveProfileMain();
            var ids = keys.Select(k => GameData.LocationsByKey[k]).ToList();
            Log.Info($"Check: world(s) {string.Join(",", worlds)} cleared as {hero} on {difficulty} ({why}): " +
                     string.Join(", ", ids.Select(l => l.Name)) + (ProfileGuard.Bound ? "" : " - offline, sent on reconnect"));
            ApClient.SendLocations(ids.Select(l => l.Id).ToList());
        }

        // Every check recorded in the bound profile: completed achievements plus recorded world clears.
        public static void ResendAll()
        {
            if (!ProfileGuard.Bound) return;
            var ids = new List<long>();
            foreach (var pair in DewSave.profileMain.achievements)
                if (pair.Value != null && pair.Value.isCompleted && GameData.LocationsByKey.TryGetValue(pair.Key, out var loc))
                    ids.Add(loc.Id);
            int achievements = ids.Count;
            foreach (var key in ApRecords.Clears())
                if (GameData.LocationsByKey.TryGetValue(key, out var loc))
                    ids.Add(loc.Id);
            Log.Info($"Resending {ids.Count} checks ({achievements} achievements, {ids.Count - achievements} world clears)");
            ApClient.SendLocations(ids);
        }
    }
}
