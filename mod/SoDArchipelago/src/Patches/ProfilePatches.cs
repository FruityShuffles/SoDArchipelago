using HarmonyLib;

namespace SoDArchipelago.Patches
{
    // DewProfile.Validate runs on every profile load and re-locks/unlocks achievement targets from achievement
    // completion. On a marked profile, put the AP-managed unlocks back to what AP has granted (see UnlockState).
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
    internal static class ValidatePatch
    {
        private static void Postfix(DewProfile __instance)
        {
            if (ProfileGuard.IsMarked(__instance)) UnlockState.Enforce(__instance, "profile validate");
        }
    }

    // DESIGN.md "Save profile binding" step 6: re-check the binding whenever the loaded profile changes.
    [HarmonyPatch(typeof(DewSave), nameof(DewSave.LoadProfile), typeof(string))]
    internal static class LoadProfilePatch
    {
        private static void Postfix() => ArchipelagoMod.OnProfileLoaded("loaded");
    }

    [HarmonyPatch(typeof(DewSave), nameof(DewSave.CreateProfile))]
    internal static class CreateProfilePatch
    {
        private static void Postfix() => ArchipelagoMod.OnProfileLoaded("created");
    }

    [HarmonyPatch(typeof(DewSave), nameof(DewSave.ConvertProfile))]
    internal static class ConvertProfilePatch
    {
        private static void Postfix() => ArchipelagoMod.OnProfileLoaded("converted");
    }
}
