using System;
using System.Collections.Generic;

namespace SoDArchipelago
{
    // On a marked profile, the lock status of every Traveler and every vanilla achievement unlock (and of a locked
    // Traveler's own memories) follows the AP unlock record, never achievement completion.
    //
    // Why this is needed: DewProfile.Validate runs on every profile load and re-derives unlocks from achievements. It
    // locks the target of every incomplete achievement (so AP unlocks would be lost) and unlocks the target of every
    // completed one (so the vanilla reward would leak), and it unlocks vanilla's starting Travelers (Lacerta, Mist).
    // DewProfile.UnlockHero also unlocks the Traveler's alternate memories whose achievement is complete. Enforce undoes
    // all of it, using the game's own Unlock*/Lock* functions.
    public static class UnlockState
    {
        // Profiles bound before random starting Travelers (no ApRecords.FormatFlag) have no unlock record for vanilla's
        // starting Travelers; those stay unlocked there.
        private static readonly string[] VanillaStarters = { "Hero_Lacerta", "Hero_Mist" };

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
            bool legacy = !ApRecords.HasFormatFlag(p);

            foreach (var t in GameData.Travelers.Values)
            {
                if (!p.heroes.ContainsKey(t.Key)) continue; // not in this build
                if (ApRecords.HasUnlock(p, t.Key) || legacy && Array.IndexOf(VanillaStarters, t.Key) >= 0)
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
                // After UnlockHero, which may have unlocked alternate memories whose achievement is complete.
                foreach (var memory in t.AltMemories) Match(p, memory, changes);
            }

            foreach (var target in GameData.UnlockTargets)
            {
                if (target.StartsWith("Hero_") || IsAltMemory(target)) continue;
                Match(p, target, changes);
            }

            RepairPreferredHeroes(p, changes);

            if (changes.Count > 0)
                Log.Info($"Unlock state enforced ({why}) on '{p.name}': {string.Join(", ", changes)}");
            return changes.Count;
        }

        // The first unlocked Traveler in the game's own order, or null if none is (only while a bind is incomplete).
        public static string FirstUnlockedHero(DewProfile p)
        {
            foreach (var hero in Dew.HeroOrder)
                if (p.heroes.ContainsKey(hero) && !IsLocked(p.heroes, hero)) return hero;
            return null;
        }

        public static bool IsHeroLocked(DewProfile p, string hero) => IsLocked(p.heroes, hero);

        // The lobby spawns each lobby type's preferred Traveler without a lock check (DESIGN.md "Received items"), so a
        // locked one is replaced in every saved entry. Entries created later are covered by the CmdSetHeroType patch.
        private static void RepairPreferredHeroes(DewProfile p, List<string> changes)
        {
            if (p.preferredGameSettings == null) return;
            string replacement = null;
            foreach (var kv in p.preferredGameSettings)
            {
                var settings = kv.Value;
                if (settings == null || settings.hero != null && !IsLocked(p.heroes, settings.hero)) continue;
                replacement = replacement ?? FirstUnlockedHero(p);
                if (replacement == null) return;
                changes.Add($"preferred[{kv.Key}] {settings.hero}->{replacement}");
                settings.hero = replacement;
            }
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
