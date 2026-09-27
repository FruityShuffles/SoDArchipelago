using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SoDArchipelago.Patches
{
    // AchievementManager.CompleteAchievement on a marked profile: send the check, and suppress only the vanilla unlock
    // loop (the AP item replaces it). Everything else in the method runs unchanged: the completion record, the
    // grantedStardust bonus (CmdRequestStardust), the chat message, the notification, the save. The Steam calls in it
    // are blocked by SteamPatches.
    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.CompleteAchievement))]
    internal static class CompleteAchievementPatch
    {
        // True only while CompleteAchievement runs on a marked profile; UnlockSuppressionPatches read it. Main thread only.
        internal static bool SuppressUnlocks;
        private static string _pendingKey;

        private static void Prefix(DewAchievementItem item)
        {
            _pendingKey = null;
            SuppressUnlocks = ProfileGuard.Marked;
            if (item == null || !SuppressUnlocks) return;
            if (DewSave.profileMain.achievements.TryGetValue(item.name, out var data) && data != null && !data.isCompleted)
                _pendingKey = item.name;
        }

        // Finalizer: runs even if the method throws after recording the completion (e.g. no local player).
        private static Exception Finalizer(Exception __exception)
        {
            SuppressUnlocks = false;
            var key = _pendingKey;
            _pendingKey = null;
            try
            {
                if (key != null && DewSave.profileMain.achievements.TryGetValue(key, out var data) && data != null &&
                    data.isCompleted)
                    CheckHandler.OnAchievementCompleted(key);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            return __exception;
        }
    }

    // The unlock loop in CompleteAchievement calls these; skip them while SuppressUnlocks is set.
    [HarmonyPatch]
    internal static class UnlockSuppressionPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DewProfile), nameof(DewProfile.UnlockHero), new[] { typeof(string) });
            yield return AccessTools.Method(typeof(DewProfile), nameof(DewProfile.UnlockSkill), new[] { typeof(string) });
            yield return AccessTools.Method(typeof(DewProfile), nameof(DewProfile.UnlockGem), new[] { typeof(string) });
            yield return AccessTools.Method(typeof(DewProfile), nameof(DewProfile.UnlockLucidDream),
                new[] { typeof(string) });
        }

        private static bool Prefix(string __0)
        {
            if (!CompleteAchievementPatch.SuppressUnlocks) return true;
            Log.Info($"Suppressed vanilla achievement unlock of {__0} (an AP item unlocks it)");
            return false;
        }
    }
}
