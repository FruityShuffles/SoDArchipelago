using System;
using HarmonyLib;

namespace SoDArchipelago.Patches
{
    // DESIGN.md "Souvenirs": owning a shop souvenir is its check, however it was unlocked (a lizard shop purchase, or a
    // redeem code, which also takes it out of the shop pool for good). The souvenir itself is untouched. The game's own
    // unlock state is the record; CheckHandler.ResendAll rebuilds the list from it on connect.
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.UnlockAccessory))]
    internal static class UnlockAccessoryPatch
    {
        // __state: a marked profile's souvenir that isn't owned yet (the binding guard comes first: no other profile is
        // read). The shop and redeem codes never unlock an owned one again, but an ownership-key refresh would.
        private static void Prefix(DewProfile __instance, string accName, out bool __state)
        {
            __state = false;
            try
            {
                __state = ProfileGuard.Marked && __instance == DewSave.profileMain && accName != null &&
                          GameData.LocationsByKey.TryGetValue(accName, out var loc) && loc.Kind == "souvenir" &&
                          !IsOwned(__instance, accName);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private static void Postfix(DewProfile __instance, string accName, bool __state)
        {
            try
            {
                if (__state && IsOwned(__instance, accName)) CheckHandler.OnSouvenirOwned(accName);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private static bool IsOwned(DewProfile profile, string accName) =>
            profile.accessories != null && profile.accessories.TryGetValue(accName, out var data) &&
            data != null && data.isUnlocked;
    }
}
