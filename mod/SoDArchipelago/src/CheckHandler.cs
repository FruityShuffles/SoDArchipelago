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
        // Shared entry point for #8/#9. Unknown keys and vanilla profiles never receive an AP record.
        public static bool RecordCheck(string locationKey)
        {
            if (!ProfileGuard.Marked || !GameData.LocationsByKey.TryGetValue(locationKey, out var loc) ||
                (loc.Kind != "ware" && loc.Kind != "shrine" && loc.Kind != "quest")) return false;
            if (!ApRecords.AddCheck(locationKey)) return false;
            DewSave.SaveProfileMain();
            Log.Info($"Check: {loc.Name} ({locationKey}){(ProfileGuard.Bound ? "" : " - offline, sent on reconnect")}");
            ApClient.SendLocations(new[] { loc.Id });
            return true;
        }

        // The successful-use RPC runs on every client; only the shrine's actual local user gets the check.
        public static void OnShrineUsed(Shrine shrine, Entity user)
        {
            if (!ProfileGuard.Marked || shrine == null || user == null || DewPlayer.local == null ||
                user.owner != DewPlayer.local) return;
            string key = shrine.GetType().Name;
            if (GameData.LocationsByKey.TryGetValue(key, out var location) && location.Kind == "shrine")
                RecordCheck(key);
        }

        // Shared quests count for all roles. Also called after client SyncVar deserialization: Actor's inactive hook
        // can fire OnQuestRemoved before DewQuest's Completed state in the same packet has been read.
        public static void OnQuestRemoved(DewQuest quest)
        {
            if (!ProfileGuard.Marked || quest == null || quest.state != QuestState.Completed) return;
            string key = quest.GetType().Name;
            if (GameData.LocationsByKey.TryGetValue(key, out var location) && location.Kind == "quest")
                RecordCheck(key);
        }

        public static bool IsArtifactDiscovered(DewProfile profile, string artifactKey) =>
            ProfileGuard.Marked && profile == DewSave.profileMain && artifactKey != null &&
            profile.artifacts.TryGetValue(artifactKey, out var data) && data != null && data.status == UnlockStatus.Complete;

        // DiscoverArtifact's before/after flag is the duplicate guard. No AP:check record: the native journal entry
        // is saved and travels with the profile, and is read again on reconnect.
        public static void OnArtifactDiscovered(DewProfile profile, string artifactKey, bool wasDiscovered)
        {
            if (wasDiscovered || !IsArtifactDiscovered(profile, artifactKey) ||
                !GameData.LocationsByKey.TryGetValue(artifactKey, out var location) || location.Kind != "artifact") return;
            DewSave.SaveProfileMain();
            Log.Info($"Check: {location.Name} ({artifactKey}){(ProfileGuard.Bound ? "" : " - offline, sent on reconnect")}");
            ApClient.SendLocations(new[] { location.Id });
        }

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

        // UnlockAccessoryPatch, when a marked profile newly owns a shop souvenir (DESIGN.md "Souvenirs").
        public static void OnSouvenirOwned(string accessoryKey)
        {
            var loc = GameData.LocationsByKey[accessoryKey];
            Log.Info($"Check: {loc.Name} ({accessoryKey}){(ProfileGuard.Bound ? "" : " - offline, sent on reconnect")}");
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
            if (!ForcedDreams.RunCounts($"world {world} clear")) return;
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

        // Every check recorded in the bound profile: achievements, world clears, souvenirs, artifacts and AP records.
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
            int clears = ids.Count - achievements;
            foreach (var loc in GameData.Souvenirs)
                if (DewSave.profileMain.accessories.TryGetValue(loc.Key, out var data) && data != null && data.isUnlocked)
                    ids.Add(loc.Id);
            int souvenirs = ids.Count - achievements - clears;
            foreach (var loc in GameData.Artifacts)
                if (IsArtifactDiscovered(DewSave.profileMain, loc.Key)) ids.Add(loc.Id);
            int artifacts = ids.Count - achievements - clears - souvenirs;
            foreach (var key in ApRecords.Checks())
                if (GameData.LocationsByKey.TryGetValue(key, out var loc))
                    ids.Add(loc.Id);
            Log.Info($"Resending {ids.Count} checks ({achievements} achievements, {clears} world clears, " +
                     $"{souvenirs} souvenirs, {artifacts} artifacts, " +
                     $"{ids.Count - achievements - clears - souvenirs - artifacts} recorded checks)");
            ApClient.SendLocations(ids);
        }
    }
}
