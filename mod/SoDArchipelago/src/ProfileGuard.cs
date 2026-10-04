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

        // True after DewSave.LoadProfile returned false, until the next successful load/create/convert. A failed load can
        // leave a mix of two profiles loaded (e.g. the new main file with the old stats), so AP must not act on it.
        public static bool LoadFailed { get; private set; }

        // Marked: the loaded profile is a real, successfully loaded profile bound to some seed/slot. The Transient
        // profile (profileMainPath == null) never counts, even if it somehow carries a marker.
        public static bool Marked =>
            !LoadFailed && DewSave.profileMainPath != null && DewSave.profileMain != null && IsMarked(DewSave.profileMain);

        // The raw marker check, without the load-validity condition. Only for the Steam write block: it must hold while
        // DewSave.LoadProfile is still running (SyncAchievements is called inside it, before our postfix can clear
        // LoadFailed), and blocking Steam writes is the safe side anyway.
        public static bool MarkerPresent => DewSave.profileMain != null && IsMarked(DewSave.profileMain);

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

        // Writes the marker into the loaded profile (DESIGN.md step 4). Called only after the AP login for this seed/slot
        // succeeded (ApClient.OnLoggedIn), so a wrong slot or password never marks a profile. The caller saves the main
        // profile once the received items (the starting Travelers) are applied, so it's never saved without them.
        public static bool Bind(string marker)
        {
            if (LoadFailed || DewSave.profileMainPath == null || DewSave.profileMain == null)
            {
                Log.Info("Can't bind the transient profile or a profile that failed to load.");
                return false;
            }
            var existing = MarkerOf(DewSave.profileMain);
            if (existing != null)
            {
                Log.Info($"Refusing to bind: this profile is already bound ({existing}).");
                return existing == marker;
            }
            DewSave.profileMain.experienceFlags.Add(marker);
            DewSave.profileMain.experienceFlags.Add(ApRecords.FormatFlag);
            UnlockState.Enforce(DewSave.profileMain, "bind");
            // Stats-recovery events from before the binding are none of AP's business (ApRecords.CheckStatsRecovery).
            if (ApRecords.AcknowledgeStatsRecoveries() > 0) DewSave.SaveProfileStats(immediate: true);
            Log.Info($"Bound profile '{DewSave.profileMain.name}' ({DewSave.profileMainPath}) to {marker}");
            return true;
        }

        // Mod load: the startup profile load ran before our patches existed, and the game ignores its result. Nothing was
        // loaded before it, so a load that failed partway leaves the main profile or the stats missing, except when
        // DewProfileStats.Validate threw after the stats were assigned. Validate only fills in missing defaults, so on a
        // marked profile it is run again here: harmless on a good profile, and it throws again on the broken one.
        // (Unmarked profiles get only the null check: AP code doesn't touch them.)
        internal static void CheckStartupLoad()
        {
            bool ok = DewSave.profileMain != null && DewSave.profileStats != null;
            if (ok && IsMarked(DewSave.profileMain))
            {
                try
                {
                    DewSave.profileStats.Validate();
                }
                catch (Exception e)
                {
                    Log.Warn("The loaded profile's stats don't validate: " + e.Message);
                    ok = false;
                }
            }
            LoadFailed = !ok;
            if (LoadFailed) Log.Warn("The startup profile load looks incomplete; AP stays off until a profile loads " +
                                     "successfully.");
        }

        // DewSave.LoadProfile / CreateProfile / ConvertProfile postfix. `ok` is LoadProfile's result (always true for the
        // other two).
        internal static void OnProfileLoaded(string how, bool ok)
        {
            _generation++;
            LoadFailed = !ok;
            Log.Info($"Profile {how}{(ok ? "" : " FAILED")}: '{DewSave.profileMain?.name}' " +
                     $"path={DewSave.profileMainPath ?? "<transient>"} marker={MarkerOf(DewSave.profileMain) ?? "<none>"}");
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
        private const string ForcedPrefix = "AP:forced:";
        private const string StarPrefix = "AP:star:";

        // Written at bind time since random starting Travelers: every Traveler comes from the unlock record. A marked
        // profile without it was bound earlier and keeps Lacerta and Mist (UnlockState).
        public const string FormatFlag = "AP:format:2";

        public static bool HasFormatFlag(DewProfile p) => p.experienceFlags.Contains(FormatFlag);

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

        // Forced Lucid Dreams (DESIGN.md "Forced Lucid Dreams"): the seed's forced list, copied from slot_data on every
        // login so forcing also works offline. A dream stays forced until its unlock is in the unlock record.
        public static List<string> Forced() =>
            ProfileGuard.Marked
                ? MainFlags.Where(f => f.StartsWith(ForcedPrefix, StringComparison.Ordinal))
                    .Select(f => f.Substring(ForcedPrefix.Length)).ToList()
                : new List<string>();

        public static bool IsForced(string lucidDream) =>
            ProfileGuard.Marked && MainFlags.Contains(ForcedPrefix + lucidDream);

        // Replaces the forced list; true if it changed.
        public static bool SetForced(ICollection<string> lucidDreams)
        {
            if (!ProfileGuard.Marked) return false;
            var current = Forced();
            if (current.Count == lucidDreams.Count && lucidDreams.All(current.Contains)) return false;
            MainFlags.RemoveAll(f => f.StartsWith(ForcedPrefix, StringComparison.Ordinal));
            foreach (var key in lucidDreams) MainFlags.Add(ForcedPrefix + key);
            return true;
        }

        // Shuffled star requirements (DESIGN.md "Shuffled star requirements"): the seed's level per star key, copied from
        // slot_data on every login so it also applies offline. Empty when the option is off.
        public static Dictionary<string, int> Stars()
        {
            var levels = new Dictionary<string, int>();
            if (!ProfileGuard.Marked) return levels;
            foreach (var f in MainFlags)
            {
                if (!f.StartsWith(StarPrefix, StringComparison.Ordinal)) continue;
                int sep = f.LastIndexOf('=');
                if (sep > StarPrefix.Length && int.TryParse(f.Substring(sep + 1), out var level))
                    levels[f.Substring(StarPrefix.Length, sep - StarPrefix.Length)] = level;
            }
            return levels;
        }

        // Replaces the star levels; true if they changed.
        public static bool SetStars(IDictionary<string, int> levels)
        {
            if (!ProfileGuard.Marked) return false;
            var current = Stars();
            if (current.Count == levels.Count && levels.All(kv => current.TryGetValue(kv.Key, out var l) && l == kv.Value))
                return false;
            MainFlags.RemoveAll(f => f.StartsWith(StarPrefix, StringComparison.Ordinal));
            foreach (var kv in levels) MainFlags.Add(StarPrefix + kv.Key + "=" + kv.Value);
            return true;
        }

        // Passive mastery off (DESIGN.md "Passive mastery"): copied from slot_data on every login so it also applies
        // offline. Only "off" is recorded, so an older seed or an unrecorded profile keeps vanilla mastery.
        private const string NoPassiveMasteryFlag = "AP:nopassivemastery";

        public static bool NoPassiveMastery() => ProfileGuard.Marked && MainFlags.Contains(NoPassiveMasteryFlag);

        // True if it changed.
        public static bool SetNoPassiveMastery(bool off)
        {
            if (!ProfileGuard.Marked || NoPassiveMastery() == off) return false;
            if (off) MainFlags.Add(NoPassiveMasteryFlag);
            else MainFlags.RemoveAll(f => f == NoPassiveMasteryFlag);
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

        // Known limitation (DESIGN.md "Received items"): at startup, before mods load, the game's stats rollback check
        // (DewSave, via DewProfileStats.GetRecoveryDelta) can restore lost mastery levels without restoring the AP
        // mastery counters kept next to them, so those packs would be applied again. There's no exact repair, so each new
        // recovery (a "loss_..." entry the game adds to recoveredLossPoints) is reported once. Returns the new entries
        // and marks them as seen.
        private const string LossPrefix = "loss_";
        private const string SeenLossPrefix = "AP:seenloss:";

        public static List<string> CheckStatsRecovery()
        {
            var found = new List<string>();
            if (!ProfileGuard.Marked) return found;
            var list = StatsFlags;
            foreach (var f in list.ToList())
                if (f.StartsWith(LossPrefix, StringComparison.Ordinal) && !list.Contains(SeenLossPrefix + f))
                {
                    found.Add(f);
                    list.Add(SeenLossPrefix + f);
                }
            return found;
        }

        // At bind time: every recovery so far happened before AP, so mark them all as seen without reporting them.
        public static int AcknowledgeStatsRecoveries() => CheckStatsRecovery().Count;
    }

    public static class Log
    {
        public static void Info(string msg) => Debug.Log("[AP] " + msg);
        public static void Warn(string msg) => Debug.LogWarning("[AP] " + msg);
    }
}
