using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SoDArchipelago
{
    // The single binding guard (DESIGN.md "Save profile binding"). Every AP action that reads or writes a profile checks
    // one of its two levels first:
    //  - Marked: the loaded profile is a real profile bound to some AP seed/slot. It gets AP behaviour (reward
    //    suppression, gating, recording checks), even offline.
    //  - Bound: Marked for exactly the seed/slot of the logged-in session. Needed to send checks and apply items.
    // An unmarked profile always gets pure vanilla behaviour.
    public static class ProfileGuard
    {
        public const string MarkerPrefix = "Archipelago:";

        // Bumped whenever a profile is loaded or created. Work queued from the socket thread is tagged with it, and
        // dropped if the profile changed before it ran.
        private static volatile int _generation;
        public static int Generation => _generation;

        // Marker of the logged-in session, or null when not logged in.
        public static string SessionMarker { get; private set; }

        public static event Action ProfileChanged;

        public static string Marker(string seed, string slot) => MarkerPrefix + seed + ":" + slot;

        public static string MarkerOf(DewProfile p)
        {
            if (p?.experienceFlags == null) return null;
            foreach (var flag in p.experienceFlags)
                if (flag != null && flag.StartsWith(MarkerPrefix, StringComparison.Ordinal))
                    return flag;
            return null;
        }

        public static bool IsMarked(DewProfile p) => MarkerOf(p) != null;

        // The Transient profile (profileMainPath == null) is never treated as bound, even if it somehow carries a marker.
        public static bool Marked =>
            DewSave.profileMainPath != null && DewSave.profileMain != null && IsMarked(DewSave.profileMain);

        public static bool Bound =>
            SessionMarker != null && Marked && MarkerOf(DewSave.profileMain) == SessionMarker;

        internal static void SetSessionMarker(string marker) => SessionMarker = marker;

        // DESIGN.md step 3: only a fresh profile can be bound (no completed achievements, no runs played).
        public static bool IsFresh(out string details)
        {
            var p = DewSave.profileMain;
            var s = DewSave.profileStats;
            int completed = p.achievements.Count(a => a.Value != null && a.Value.isCompleted);
            int results = (p.lastGameResults?.Count ?? 0) + (p.recentlyConcededGames?.Count ?? 0);
            long plays = s?.heroes?.Values.Sum(h => h?.playCount ?? 0) ?? 0;
            details = $"completed achievements={completed}, recorded runs={results}, Traveler play count={plays}, " +
                      $"play time={p.totalPlayTimeMinutes} min, didPlayTutorial={p.didPlayTutorial}";
            return completed == 0 && results == 0 && plays == 0;
        }

        // Writes the marker into the loaded profile (DESIGN.md step 4) and saves it right away.
        public static bool Bind(string marker)
        {
            if (DewSave.profileMainPath == null || DewSave.profileMain == null)
            {
                Log.Info("Can't bind the transient profile. Create a profile first.");
                return false;
            }
            var existing = MarkerOf(DewSave.profileMain);
            if (existing != null)
            {
                Log.Info($"Refusing to bind: this profile is already bound ({existing}).");
                return existing == marker;
            }
            DewSave.profileMain.experienceFlags.Add(marker);
            UnlockState.Enforce(DewSave.profileMain, "bind");
            DewSave.SaveProfileMain(immediate: true);
            Log.Info($"Bound profile '{DewSave.profileMain.name}' ({DewSave.profileMainPath}) to {marker}");
            return true;
        }

        // DewSave.LoadProfile / CreateProfile / ConvertProfile postfix.
        internal static void OnProfileLoaded(string how)
        {
            _generation++;
            Log.Info($"Profile {how}: '{DewSave.profileMain?.name}' path={DewSave.profileMainPath ?? "<transient>"} " +
                     $"marker={MarkerOf(DewSave.profileMain) ?? "<none>"}");
            ProfileChanged?.Invoke();
        }
    }

    // AP records kept in the bound profile. They live in string lists the game only reads with Contains
    // (DewProfile.experienceFlags, DewProfileStats.recoveredLossPoints), so they are saved with the profile and travel
    // with it. Unknown JSON fields would be dropped on the next save, so no new fields are added.
    // Every method checks ProfileGuard.Marked itself.
    public static class ApRecords
    {
        private const string ClearPrefix = "AP:clear:";
        private const string WinPrefix = "AP:win:";
        private const string UnlockPrefix = "AP:unlock:";
        private const string AppliedPrefix = "AP:applied:";

        private static List<string> MainFlags => DewSave.profileMain.experienceFlags;

        private static List<string> StatsFlags =>
            DewSave.profileStats.recoveredLossPoints ?? (DewSave.profileStats.recoveredLossPoints = new List<string>());

        // Unlock record: which unlock targets AP has granted. Read from the given profile, because the Validate patch
        // runs on a profile that isn't DewSave.profileMain yet.
        public static bool HasUnlock(DewProfile p, string target) =>
            ProfileGuard.IsMarked(p) && p.experienceFlags.Contains(UnlockPrefix + target);

        public static bool AddUnlock(string target)
        {
            if (!ProfileGuard.Marked || MainFlags.Contains(UnlockPrefix + target)) return false;
            MainFlags.Add(UnlockPrefix + target);
            return true;
        }

        public static IEnumerable<string> Clears() =>
            ProfileGuard.Marked
                ? MainFlags.Where(f => f.StartsWith(ClearPrefix, StringComparison.Ordinal))
                    .Select(f => f.Substring(ClearPrefix.Length)).ToList()
                : Enumerable.Empty<string>();

        public static bool AddClear(string locationKey)
        {
            if (!ProfileGuard.Marked || MainFlags.Contains(ClearPrefix + locationKey)) return false;
            MainFlags.Add(ClearPrefix + locationKey);
            return true;
        }

        public static bool AddWin(string travelerKey, string difficultyId)
        {
            var flag = WinPrefix + travelerKey + ":" + difficultyId;
            if (!ProfileGuard.Marked || MainFlags.Contains(flag)) return false;
            MainFlags.Add(flag);
            return true;
        }

        public static IEnumerable<(string traveler, string difficultyId)> Wins()
        {
            if (!ProfileGuard.Marked) yield break;
            foreach (var f in MainFlags.ToList())
            {
                if (!f.StartsWith(WinPrefix, StringComparison.Ordinal)) continue;
                var rest = f.Substring(WinPrefix.Length);
                int sep = rest.LastIndexOf(':');
                if (sep > 0) yield return (rest.Substring(0, sep), rest.Substring(sep + 1));
            }
        }

        // Applied counters for filler: how many copies of an item have already been applied. Stored in the same file as
        // the value they protect (Stardust: main profile; mastery: the stats file), so one save writes both.
        public static int GetApplied(bool inStats, string itemKey)
        {
            if (!ProfileGuard.Marked) return int.MaxValue;
            var prefix = AppliedPrefix + itemKey + "=";
            foreach (var f in inStats ? StatsFlags : MainFlags)
                if (f.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(f.Substring(prefix.Length), out var n))
                    return n;
            return 0;
        }

        public static void SetApplied(bool inStats, string itemKey, int count)
        {
            if (!ProfileGuard.Marked) return;
            var list = inStats ? StatsFlags : MainFlags;
            var prefix = AppliedPrefix + itemKey + "=";
            list.RemoveAll(f => f.StartsWith(prefix, StringComparison.Ordinal));
            list.Add(prefix + count);
        }
    }

    public static class Log
    {
        public static void Info(string msg) => Debug.Log("[AP] " + msg);
        public static void Warn(string msg) => Debug.LogWarning("[AP] " + msg);
    }
}
