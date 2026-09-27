using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Steamworks;

namespace SoDArchipelago.Patches
{
    // DESIGN.md "Save profile binding" step 8: a fresh AP profile must not push its (lower) achievement progress to
    // Steam. The profile -> Steam sync on load is skipped, and so is every Steam stat/achievement write while a marked
    // profile is loaded: CompleteAchievement, AchievementManager.SetPlatformStats and
    // DewAchievementItem.FlushProgressToProfile all write through these two methods. Steam is never read back.
    [HarmonyPatch(typeof(DewSave), nameof(DewSave.SyncAchievements))]
    internal static class SyncAchievementsPatch
    {
        private static bool Prefix()
        {
            if (!ProfileGuard.Marked) return true;
            Log.Info("Skipped the Steam achievement sync (Archipelago profile)");
            return false;
        }
    }

    [HarmonyPatch]
    internal static class SteamUserStatsPatches
    {
        private static bool _logged;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SteamUserStats), nameof(SteamUserStats.SetStat),
                new[] { typeof(string), typeof(int) });
            yield return AccessTools.Method(typeof(SteamUserStats), nameof(SteamUserStats.SetStat),
                new[] { typeof(string), typeof(float) });
            yield return AccessTools.Method(typeof(SteamUserStats), nameof(SteamUserStats.SetAchievement),
                new[] { typeof(string) });
        }

        private static bool Prefix(string __0, ref bool __result)
        {
            if (!ProfileGuard.Marked) return true;
            if (!_logged)
            {
                _logged = true;
                Log.Info($"Blocking Steam stat/achievement writes while an Archipelago profile is loaded (first: {__0})");
            }
            __result = true;
            return false;
        }
    }
}
