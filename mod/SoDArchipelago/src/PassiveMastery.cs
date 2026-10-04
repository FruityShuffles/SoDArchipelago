namespace SoDArchipelago
{
    // Passive mastery (DESIGN.md "Passive mastery"). With the option off, runs award no Traveler mastery, so mastery only
    // comes from Mastery items. The run's reward itself becomes 0 (MasteryPatches), so the results screen skips its mastery
    // panel, as it does for any run that earned nothing. Each player's client rewards their own profile, so in co-op this
    // only affects this player.
    public static class PassiveMastery
    {
        // Login: copy the seed's setting into the profile, so it also applies offline. A seed from before the option has
        // no key: passive mastery stays on.
        public static void OnLoggedIn()
        {
            if (!ProfileGuard.Bound) return;
            bool on = ApClient.GetBool("passive_mastery", fallback: true);
            if (ApRecords.SetNoPassiveMastery(!on)) DewSave.SaveProfileMain();
            Log.Info("Passive mastery: " + (on ? "on" : "off"));
        }

        public static bool Off => ApRecords.NoPassiveMastery();
    }
}
