using System;
using Archipelago.MultiClient.Net.Models;
using UnityEngine;

namespace SoDArchipelago
{
    public static class MapBlessings
    {
        public static void Initialize() => InRunItems.Register("blessing", TryLand, LandingNotice);

        // InRunItems supplies the bound, online, ready solo/host run guard. Use vanilla's treasure-map
        // selection and AddModifier path, which persists the modifier and spawns it when the room is entered.
        private static bool TryLand(GameData.Item item, ItemInfo info)
        {
            var zm = NetworkedManagerBase<ZoneManager>.instance;
            var pm = NetworkedManagerBase<PingManager>.instance;
            var player = DewPlayer.local;
            if (zm == null || zm.nodes.Count == 0 || pm == null || player == null || string.IsNullOrEmpty(item.Target))
                return false;
            var prefab = DewResources.GetByShortTypeName<RoomModifierBase>(item.Target);
            if (prefab == null) return false;

            // Main bonuses and the Lizard Shop must not replace another bonus. Shrines and Artifact may share it.
            var settings = new GetNodeIndexSettings
            {
                desiredDistance = new Vector2Int(prefab.isMain ? 2 : 3, 4),
                preferCloserToExit = true,
                avoidMainModifier = prefab.isMain
            };
            if (!zm.TryGetNodeIndexForNextGoal(settings, out int node)) return false;
            zm.AddModifier(node, new ModifierData { type = item.Target, isForceRevealed = true });

            // This is already the server: use the vanilla RPC with the host as sender. Unlike CmdSendPing,
            // it does not rate-limit a batch of received blessings against the player's chat allowance.
            try
            {
                pm.BroadcastPing(new PingManager.Ping { sender = player, type = PingManager.PingType.WorldNode,
                    itemIndex = node });
            }
            catch (Exception e)
            {
                // A feedback failure must not replay a modifier that has already landed.
                Debug.LogException(e);
            }
            return true;
        }

        private static string LandingNotice(GameData.Item item, ItemInfo info)
        {
            const string prefix = "Blessing: ";
            string name = item.Name.StartsWith(prefix, StringComparison.Ordinal) ? item.Name.Substring(prefix.Length) : item.Name;
            string sender = info.LocationId == -2 ? "starting items" : info.LocationId < 0 ? "server" : ApClient.PlayerName(info);
            return $"Blessing from {sender}: {name}, marked on your map.";
        }
    }
}
