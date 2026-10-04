using Mirror;

namespace SoDArchipelago
{
    // DeathLink (DESIGN.md "DeathLink"), only when the slot has death_link on:
    //  - the local Traveler being knocked down sends one;
    //  - a received one kills the local Traveler with Entity.Kill, which goes through the hero's normal death
    //    interrupt: a knockdown, or a bleed-out first on difficulties with enableBleedOuts. In co-op teammates can
    //    revive as usual;
    //  - a knockdown caused by a received DeathLink never sends another.
    public static class DeathLinkHandler
    {
        // Set when we kill our own Traveler for a received DeathLink. The next local knockdown consumes it; a revive or
        // leaving the run clears it.
        private static bool _causedByDeathLink;

        public static void Reset() => _causedByDeathLink = false;

        // ClientEventManager.OnHeroRevive.
        public static void OnHeroRevive(Hero hero)
        {
            if (DewPlayer.local != null && hero != null && hero == DewPlayer.local.hero) _causedByDeathLink = false;
        }

        // ClientEventManager.OnHeroKnockedOut; fires on every client for every hero.
        public static void OnHeroKnockedOut(Hero hero)
        {
            if (DewPlayer.local == null || hero == null || hero != DewPlayer.local.hero) return;
            if (_causedByDeathLink)
            {
                _causedByDeathLink = false;
                Log.Info("Knocked out by a received DeathLink; not sending one back.");
                return;
            }
            if (!ApClient.DeathLinkEnabled) return;
            var name = GameData.Travelers.TryGetValue(hero.GetType().Name, out var t) ? t.Name : hero.GetType().Name;
            Log.Info("Local Traveler knocked out; sending DeathLink.");
            ApClient.SendDeathLink($"{ApClient.SlotName}'s {name} was knocked out");
        }

        public static void OnDeathLinkReceived(string source, string cause)
        {
            if (!ApClient.DeathLinkEnabled) return;
            var text = string.IsNullOrEmpty(cause) ? $"DeathLink from {source}" : $"DeathLink: {cause}";
            var hero = DewPlayer.local != null ? DewPlayer.local.hero : null;
            if (NetworkedManagerBase<GameManager>.instance == null || hero == null || !hero.isActive)
            {
                ApClient.Say(text + " (not in a run, ignored)");
                return;
            }
            if (hero.isKnockedOut)
            {
                ApClient.Say(text + " (already knocked out)");
                return;
            }
            if (!NetworkServer.active)
            {
                // Entity.Kill is server-only and the game has no client command that kills your own Traveler.
                ApClient.Say(text + " (can't be applied: only the host can kill Travelers)");
                return;
            }
            ApClient.Say(text);
            _causedByDeathLink = true;
            hero.Kill();
            // A Determination Shard (or another saving interrupt) absorbs the kill without a knockout.
            // Bleed-out still leads to a later knockout, so keep its suppression until that event/revive.
            if (!hero.isKnockedOut && !hero.Status.HasStatusEffect<Se_HeroKnockedOut>() &&
                !hero.Status.HasStatusEffect<Se_HeroBleedingOut>())
                _causedByDeathLink = false;
        }
    }
}
