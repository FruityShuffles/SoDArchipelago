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

    // The lobby spawns the lobby type's preferred Traveler without a lock check, and a lobby type with no saved settings
    // yet defaults to Lacerta (DESIGN.md "Received items"). On a marked profile, a locked Traveler is replaced with the
    // first unlocked one, which also becomes the preference it came from.
    [HarmonyPatch(typeof(DewPlayer), nameof(DewPlayer.CmdSetHeroType))]
    internal static class SetHeroTypePatch
    {
        private static void Prefix(DewPlayer __instance, ref string newType)
        {
            if (!__instance.isLocalPlayer || !ProfileGuard.Marked) return;
            var profile = DewSave.profileMain;
            if (newType != null && !UnlockState.IsHeroLocked(profile, newType)) return;
            var replacement = UnlockState.FirstUnlockedHero(profile);
            if (replacement == null)
            {
                Log.Warn($"No Traveler is unlocked; leaving {newType} as the lobby Traveler.");
                return;
            }
            var preferred = NetworkedManagerBase<GameSettingsManager>.instance?.GetLocalPreferredGameSettings();
            if (preferred != null && preferred.hero == newType)
            {
                preferred.hero = replacement;
                DewSave.SaveProfileMain();
            }
            Log.Info($"{newType} is locked; the lobby Traveler is {replacement} instead.");
            newType = replacement;
        }
    }

    // DESIGN.md "Save profile binding" step 6: re-check the binding whenever the loaded profile changes. LoadProfile can
    // fail partway (returns false) after replacing some of the loaded profile's parts; that is reported as a failed load.
    [HarmonyPatch(typeof(DewSave), nameof(DewSave.LoadProfile), typeof(string))]
    internal static class LoadProfilePatch
    {
        private static void Postfix(bool __result) => ArchipelagoMod.OnProfileLoaded("loaded", __result);
    }

    [HarmonyPatch(typeof(DewSave), nameof(DewSave.CreateProfile))]
    internal static class CreateProfilePatch
    {
        private static void Postfix() => ArchipelagoMod.OnProfileLoaded("created", true);
    }

    // ConvertProfile returns early, changing nothing, when the file isn't convertible; only report a real conversion.
    [HarmonyPatch(typeof(DewSave), nameof(DewSave.ConvertProfile))]
    internal static class ConvertProfilePatch
    {
        private static void Prefix(out DewProfile __state) => __state = DewSave.profileMain;

        private static void Postfix(DewProfile __state)
        {
            if (!ReferenceEquals(__state, DewSave.profileMain)) ArchipelagoMod.OnProfileLoaded("converted", true);
        }
    }
}
