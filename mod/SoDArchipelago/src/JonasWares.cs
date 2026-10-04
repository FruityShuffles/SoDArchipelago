using System;
using System.Collections.Generic;
using System.Linq;

namespace SoDArchipelago
{
    // Vanilla Treasure stock carries the check key over Mirror. Only the host's own Jonas stock is modified.
    public static class JonasWares
    {
        public const string Placeholder = "Treasure_CloakOfGuidance";
        private const string TagPrefix = "AP:";

        public static bool IsLocalHost(DewPlayer player) =>
            ProfileGuard.Marked && Mirror.NetworkServer.active && player != null && player == DewPlayer.local;

        public static List<GameData.Location> Enabled() =>
            ProfileGuard.Marked
                ? GameData.LocationsByKey.Values.Where(l => l.Kind == "ware" && l.Number > 0 &&
                    l.Number <= ApRecords.WareCount()).OrderBy(l => l.Number).ToList()
                : new List<GameData.Location>();

        public static bool TryGetWare(MerchandiseData data, out GameData.Location location)
        {
            location = null;
            return data.type == MerchandiseType.Treasure && data.itemName == Placeholder &&
                   data.customData != null && data.customData.StartsWith(TagPrefix, StringComparison.Ordinal) &&
                   GameData.LocationsByKey.TryGetValue(data.customData.Substring(TagPrefix.Length), out location) &&
                   location.Kind == "ware";
        }

        public static bool IsAvailable(GameData.Location location) =>
            ProfileGuard.Marked && location.Number > 0 && location.Number <= ApRecords.WareCount() &&
            !ApRecords.HasCheck(location.Key);

        public static void OnLoggedIn()
        {
            if (!ProfileGuard.Bound) return;
            if (ApRecords.SetWareCount(ApClient.GetInt("jonas_wares", 0))) DewSave.SaveProfileMain();
            ApClient.ScoutWares(Enabled());
        }

        // Called on the main thread through ApClient's connection/profile-tagged queue.
        public static void CacheScouts(IDictionary<string, (string item, string owner)> contents)
        {
            if (!ProfileGuard.Bound) return;
            var enabled = new HashSet<string>(Enabled().Select(l => l.Key));
            bool changed = false;
            foreach (var pair in contents)
                if (enabled.Contains(pair.Key)) changed |= ApRecords.SetScout(pair.Key, pair.Value.item, pair.Value.owner);
            if (changed) DewSave.SaveProfileMain();
        }

        public static void Append(PropEnt_Merchant_Base merchant, DewPlayer player)
        {
            if (!(merchant is PropEnt_Merchant_Jonas) || !IsLocalHost(player) ||
                !merchant.merchandises.TryGetValue(player.guid, out var stock)) return;
            var pending = Enabled().Where(IsAvailable).ToList();
            if (pending.Count == 0) return;
            var location = pending[UnityEngine.Random.Range(0, pending.Count)];
            var treasure = DewResources.GetByShortTypeName<Treasure>(Placeholder);
            // Cloak inherits both vanilla methods: OnAddMerchandise computes the world-scaled price and
            // CanBePurchased is always true. Its OnCreate effect never runs because SpawnMerchandise is intercepted.
            treasure.OnAddMerchandise(out var price, out _);
            var ware = new MerchandiseData { type = MerchandiseType.Treasure, itemName = Placeholder,
                price = price, count = 1, customData = TagPrefix + location.Key };
            merchant.merchandises[player.guid] = stock.Where(d => !TryGetWare(d, out _)).Concat(new[] { ware }).ToArray();
        }

        // Runs before CmdPurchase spends gold. A continued save can contain stock bought after its last save.
        public static bool CanPurchase(PropEnt_Merchant_Base merchant, DewPlayer player, int index)
        {
            if (!(merchant is PropEnt_Merchant_Jonas) || !IsLocalHost(player) ||
                !merchant.merchandises.TryGetValue(player.guid, out var stock) || index < 0 || index >= stock.Length ||
                !TryGetWare(stock[index], out var location)) return true;
            if (IsAvailable(location)) return true;
            var updated = stock.ToArray();
            updated[index].count = 0;
            merchant.merchandises[player.guid] = updated;
            return false;
        }

        // True means the spawn was handled; vanilla has already charged the gold and decremented the count.
        public static bool Consume(PropEnt_Merchant_Base merchant, DewPlayer player, MerchandiseData data)
        {
            if (!(merchant is PropEnt_Merchant_Jonas) || !IsLocalHost(player) || !TryGetWare(data, out var location))
                return false;
            if (IsAvailable(location)) CheckHandler.RecordCheck(location.Key);
            return true;
        }

        public static string Description(GameData.Location location)
        {
            if (!ProfileGuard.Marked) return "an unknown ware";
            // Offline wares remain buyable, but reveal no contents until connected again.
            return ProfileGuard.Bound && ApClient.IsConnected &&
                   ApRecords.TryScout(location.Key, out var item, out var owner)
                ? item + ", for " + owner : "an unknown ware";
        }
    }
}
