using System;
using HarmonyLib;
using UnityEngine;

namespace SoDArchipelago.Patches
{
    // The receiver implementation runs locally on hosts and joining clients, unlike the server RPC wrapper.
    [HarmonyPatch(typeof(Shrine), "UserCode_RpcInvokeOnSuccessfulUse__Entity")]
    internal static class ShrineUsePatch
    {
        private static void Postfix(Shrine __instance, Entity entity)
        {
            try { CheckHandler.OnShrineUsed(__instance, entity); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    [HarmonyPatch(typeof(DewQuest), nameof(DewQuest.DeserializeSyncVars))]
    internal static class QuestCompletionSyncPatch
    {
        private static void Postfix(DewQuest __instance)
        {
            try { CheckHandler.OnQuestRemoved(__instance); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.DiscoverArtifact))]
    internal static class ArtifactDiscoveryPatch
    {
        private static void Prefix(DewProfile __instance, string g, out bool __state)
        {
            __state = CheckHandler.IsArtifactDiscovered(__instance, g);
        }

        private static void Postfix(DewProfile __instance, string g, bool __state)
        {
            try { CheckHandler.OnArtifactDiscovered(__instance, g, __state); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
