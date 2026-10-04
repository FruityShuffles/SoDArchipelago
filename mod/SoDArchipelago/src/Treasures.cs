using System;
using Archipelago.MultiClient.Net.Models;
using UnityEngine;

namespace SoDArchipelago
{
    public static class Treasures
    {
        public static void Initialize() => InRunItems.Register("treasure", TryLand);

        // The shared loop supplies the Bound, online, ready solo/host run guard.
        private static bool TryLand(GameData.Item item, ItemInfo info)
        {
            var zm = NetworkedManagerBase<ZoneManager>.instance;
            var player = DewPlayer.local;
            var hero = player != null ? player.hero : null;
            if (zm == null || hero == null || !hero.isActive || string.IsNullOrEmpty(item.Target)) return false;
            var prefab = DewResources.GetByShortTypeName<Treasure>(item.Target);
            if (prefab == null) return false;

            switch (item.Target)
            {
                case "Treasure_CloakOfGuidance":
                    if (zm.isHuntAdvanceDisabled) return false;
                    break;
                case "Treasure_Clairvoyance":
                    // This calls the same HasNonRevealedArea predicate as CanBePurchased, without its
                    // failure message (which needs a buyer on the instance, not on the shared prefab).
                    if (!prefab.ShouldBeIncludedInPool()) return false;
                    break;
                case "Treasure_FragmentOfDetermination":
                    break;
                case "Treasure_TreasureMap":
                case "Treasure_TotallyGenuineTreasureMap":
                    if (NetworkedManagerBase<QuestManager>.instance == null || zm.nodes.Count == 0) return false;
                    bool genuine = item.Target == "Treasure_TotallyGenuineTreasureMap";
                    if (!zm.TryGetNodeIndexForNextGoal(new GetNodeIndexSettings
                    {
                        desiredDistance = new Vector2Int(genuine ? 2 : 3, 4),
                        preferCloserToExit = true,
                        avoidMainModifier = genuine
                    }, out _)) return false;
                    // StartQuest permits duplicates. Let each vanilla Treasure start its own quest and
                    // select its destination; no quest state or reward is implemented here.
                    break;
                default:
                    return false;
            }

            // Match Jonas's SpawnMerchandise: assign fields before networking invokes OnCreate.
            var spawned = Dew.InstantiateAndSpawn(prefab, hero.agentPosition, null, t =>
            {
                t.player = player;
                t.hero = hero;
                t.price = 0;
                t.merchant = null;
                t.customData = null;
            });
            if (item.Target == "Treasure_Clairvoyance" && spawned != null)
            {
                try { spawned.Destroy(); }
                catch (Exception e) { Debug.LogException(e); } // Already revealed: never replay the grant.
            }
            return true;
        }
    }
}
