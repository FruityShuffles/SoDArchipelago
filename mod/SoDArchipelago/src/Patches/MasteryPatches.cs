using System;
using HarmonyLib;

namespace SoDArchipelago.Patches
{
    // Passive mastery off (PassiveMastery): DewSave.ConsumeGameResult turns a run into mastery points with
    // Dew.GetRewardedMasteryPoints, adds them, and reports them to the results screen (heroMasteryPoints). Zeroing that one
    // call inside ConsumeGameResult makes the run worth 0 points everywhere: nothing is added, the results screen skips
    // its mastery panel, and a conceded run records 0 points to take back later. Other callers (redeem codes, the old
    // free-version reward) are left alone. ConsumeGameResult covers every way a run is rewarded: the end of a game, and a
    // run left unfinished, caught up on the title screen or in the lobby.
    [HarmonyPatch(typeof(DewSave), nameof(DewSave.ConsumeGameResult))]
    internal static class ConsumeGameResultPatch
    {
        // True only while ConsumeGameResult runs with passive mastery off. Main thread only.
        internal static bool SuppressMastery;

        private static void Prefix()
        {
            try
            {
                SuppressMastery = PassiveMastery.Off;
            }
            catch (Exception e)
            {
                SuppressMastery = false;
                UnityEngine.Debug.LogException(e);
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            SuppressMastery = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Dew), nameof(Dew.GetRewardedMasteryPoints))]
    internal static class RewardedMasteryPointsPatch
    {
        private static void Postfix(ref long __result)
        {
            if (!ConsumeGameResultPatch.SuppressMastery) return;
            Log.Info($"Run mastery reward suppressed ({__result} points): passive mastery is off");
            __result = 0;
        }
    }
}
