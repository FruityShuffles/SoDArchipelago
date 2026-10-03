using System;
using System.Collections.Generic;

namespace SoDArchipelago
{
    // Applies received AP items to the bound profile (DESIGN.md "Received items").
    //  - Unlocks are rebuilt from the full received list every time: the nth copy of a progressive item maps to the nth
    //    entry of its unlock list. The AP unlock record in the profile only grows, so receiving an item twice changes
    //    nothing, and no item index is needed.
    //  - Filler uses applied counters (ApRecords.GetApplied/SetApplied) stored in the same file as the value they
    //    protect, so one save writes both: a crash can't apply an item twice or lose it.
    public static class ItemHandler
    {
        public static void ProcessAll(string why)
        {
            if (!ProfileGuard.Bound) return;
            var profile = DewSave.profileMain;
            var stats = DewSave.profileStats;
            bool inRun = NetworkedManagerBase<GameManager>.instance != null;
            string nextRun = inRun ? " (next run)" : "";

            var copies = new Dictionary<string, int>();
            var applied = new Dictionary<string, int>();
            int stardustValue = Math.Max(1, ApClient.GetInt("stardust_pack_value", 650));
            int masteryValue = Math.Max(1, ApClient.GetInt("mastery_pack_value", 5));
            bool mainDirty = false, statsDirty = false;

            foreach (var info in ApClient.ReceivedItems)
            {
                if (!GameData.ItemsById.TryGetValue(info.ItemId, out var item))
                {
                    Log.Warn($"Received unknown item id {info.ItemId}; is the mod older than the apworld?");
                    continue;
                }
                copies.TryGetValue(item.Key, out var n);
                copies[item.Key] = ++n;
                var from = Sender(info);

                if (item.Unlocks.Count > 0)
                {
                    if (n > item.Unlocks.Count) continue;
                    var target = item.Unlocks[n - 1];
                    if (!ApRecords.AddUnlock(target)) continue;
                    mainDirty = true;
                    var what = item.UnlockNames != null ? $"{item.Name} ({item.UnlockNames[n - 1]})" : item.Name;
                    if (ApRecords.IsForced(target)) what += " - no longer forced";
                    ApClient.Say($"Received {what}{from}{nextRun}");
                    continue;
                }

                // Filler: only the copies past the applied counter are new.
                bool inStats = item.Kind == "mastery";
                if (!applied.TryGetValue(item.Key, out var done))
                    applied[item.Key] = done = ApRecords.GetApplied(inStats, item.Key);
                if (n <= done) continue;
                if (item.Kind == "stardust")
                    ApClient.Say($"Received Stardust (+{stardustValue:#,##0}){from}");
                else if (item.Kind == "mastery")
                    ApClient.Say($"Received {item.Name} (+{masteryValue} levels){from}{nextRun}");
            }

            if (mainDirty) UnlockState.Enforce(profile, "received items");

            // Stardust: value and counter both live in the main profile.
            copies.TryGetValue(GameData.StardustKey, out var stardustCopies);
            int stardustDone = ApRecords.GetApplied(false, GameData.StardustKey);
            if (stardustCopies > stardustDone)
            {
                int add = (stardustCopies - stardustDone) * stardustValue;
                profile.stardust += add;
                ApRecords.SetApplied(false, GameData.StardustKey, stardustCopies);
                mainDirty = true;
                // The constellation screen works on a copy of the Stardust total and writes it back on commit.
                var constellations = SingletonBehaviour<UI_Constellations>.instance;
                if (constellations != null && constellations.state != null) constellations.state.stardust += add;
                Log.Info($"Stardust +{add} ({stardustCopies - stardustDone} x {stardustValue}); now {profile.stardust}; " +
                         $"applied {stardustCopies}");
            }

            // Known limitation: the game's startup stats recovery may have restored mastery without our counters, in which
            // case the packs below are applied a second time. Report it once per recovery (ApRecords.CheckStatsRecovery).
            var recoveries = ApRecords.CheckStatsRecovery();
            if (recoveries.Count > 0)
            {
                statsDirty = true;
                Log.Warn("The game recovered lost mastery progress for this profile (" + string.Join(", ", recoveries) +
                         "). Mastery items received before that may be applied a second time.");
                ApClient.Say("The game restored lost mastery progress; some Mastery items may count twice.");
            }

            // Mastery: value (DewProfileStats) and counter both live in the stats file.
            foreach (var traveler in GameData.Travelers.Values)
            {
                var key = "MASTERY_" + traveler.Key;
                copies.TryGetValue(key, out var masteryCopies);
                int masteryDone = ApRecords.GetApplied(true, key);
                if (masteryCopies <= masteryDone) continue;
                if (!stats.heroes.TryGetValue(traveler.Key, out var hero) || hero == null)
                {
                    Log.Warn($"No mastery stats for {traveler.Key}; leaving {masteryCopies - masteryDone} packs for later.");
                    continue;
                }
                int levels = (masteryCopies - masteryDone) * masteryValue;
                long points = 0;
                for (int i = 0; i < levels; i++) points += Dew.GetRequiredMasteryPointsToLevelUp(hero.masteryLevel + i);
                int before = hero.masteryLevel;
                stats.AddMasteryPoints(traveler.Key, points);
                ApRecords.SetApplied(true, key, masteryCopies);
                statsDirty = true;
                Log.Info($"Mastery {traveler.Key}: +{levels} levels ({points} points), level {before} -> " +
                         $"{hero.masteryLevel} (points into level {hero.currentMasteryPoints}); applied {masteryCopies}");
            }

            if (mainDirty) DewSave.SaveProfileMain();
            if (statsDirty) DewSave.SaveProfileStats();
            if (mainDirty || statsDirty) Log.Info($"Items processed ({why}): {ApClient.ReceivedItems.Count} received");
        }

        private static string Sender(Archipelago.MultiClient.Net.Models.ItemInfo info)
        {
            var name = ApClient.PlayerName(info);
            if (info.LocationId == -2) return " (starting item)";
            if (info.LocationId < 0) return " (server)";
            return name == ApClient.SlotName ? "" : $" from {name}";
        }
    }
}
