using System;
using System.Collections.Generic;

namespace SoDArchipelago
{
    // On a marked profile, the lock status of every vanilla achievement unlock (and of a locked Traveler's own
    // memories) follows the AP unlock record, never achievement completion.
    //
    // Why this is needed: DewProfile.Validate runs on every profile load and re-derives unlocks from achievements. It
    // locks the target of every incomplete achievement (so AP unlocks would be lost) and unlocks the target of every
    // completed one (so the vanilla reward would leak). DewProfile.UnlockHero also unlocks the Traveler's alternate
    // memories whose achievement is complete. Enforce undoes both, using the game's own Unlock*/Lock* functions.
    public static class UnlockState
    {
        // Never throws: it runs inside DewProfile.Validate, and an exception there would make the profile fail to load.
        public static int Enforce(DewProfile p, string why)
        {
            try
            {
                return EnforceImpl(p, why);
            }
            catch (Exception e)
            {
                Log.Warn($"Enforcing the unlock state ({why}) failed: {e}");
                return 0;
            }
        }

        private static int EnforceImpl(DewProfile p, string why)
        {
            if (!ProfileGuard.IsMarked(p)) return 0;
            var changes = new List<string>();

            foreach (var t in GameData.Travelers.Values)
            {
                if (!p.heroes.ContainsKey(t.Key)) continue; // not in this build
                if (!t.StartsUnlocked)
                {
                    if (ApRecords.HasUnlock(p, t.Key))
                    {
                        if (IsLocked(p.heroes, t.Key))
                        {
                            p.UnlockHero(t.Key); // also unlocks the Traveler's base memories
                            changes.Add("+" + t.Key);
                        }
                    }
                    else
                    {
                        if (!IsLocked(p.heroes, t.Key))
                        {
                            p.LockHero(t.Key);
                            changes.Add("-" + t.Key);
                        }
                        foreach (var skill in t.Skills)
                        {
                            if (IsLocked(p.skills, skill)) continue;
                            p.LockSkill(skill);
                            changes.Add("-" + skill);
                        }
                    }
                }
                // After UnlockHero, which may have unlocked alternate memories whose achievement is complete.
                foreach (var memory in t.AltMemories) Match(p, memory, changes);
            }

            foreach (var target in GameData.UnlockTargets)
            {
                if (target.StartsWith("Hero_") || IsAltMemory(target)) continue;
                Match(p, target, changes);
            }

            if (changes.Count > 0)
                Log.Info($"Unlock state enforced ({why}) on '{p.name}': {string.Join(", ", changes)}");
            return changes.Count;
        }

        private static void Match(DewProfile p, string target, List<string> changes)
        {
            bool want = ApRecords.HasUnlock(p, target);
            if (target.StartsWith("St_"))
            {
                if (!p.skills.ContainsKey(target)) return;
                if (want && IsLocked(p.skills, target)) { p.UnlockSkill(target); changes.Add("+" + target); }
                else if (!want && !IsLocked(p.skills, target)) { p.LockSkill(target); changes.Add("-" + target); }
            }
            else if (target.StartsWith("Gem_"))
            {
                if (!p.gems.ContainsKey(target)) return;
                if (want && IsLocked(p.gems, target)) { p.UnlockGem(target); changes.Add("+" + target); }
                else if (!want && !IsLocked(p.gems, target)) { p.LockGem(target); changes.Add("-" + target); }
            }
            else if (target.StartsWith("LucidDream_"))
            {
                if (!p.lucidDreams.ContainsKey(target)) return;
                if (want && IsLocked(p.lucidDreams, target)) { p.UnlockLucidDream(target); changes.Add("+" + target); }
                else if (!want && !IsLocked(p.lucidDreams, target)) { p.LockLucidDream(target); changes.Add("-" + target); }
            }
        }

        private static bool IsAltMemory(string target)
        {
            foreach (var t in GameData.Travelers.Values)
                if (t.AltMemories.Contains(target)) return true;
            return false;
        }

        // A missing entry counts as locked, so nothing is done for content this build doesn't have.
        private static bool IsLocked(Dictionary<string, DewProfile.UnlockData> dict, string key) =>
            !dict.TryGetValue(key, out var data) || data == null || data.status == UnlockStatus.Locked;
    }
}
