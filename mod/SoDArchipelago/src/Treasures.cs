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
            // These effects use the remaining world map. An exit boss has no more node travel;
            // Hunter skips reset on world generation and current-world map quests miss their goal.
            // A Shard remains useful during the boss fight and survives in the native run save.
            if (item.Target != "Treasure_FragmentOfDetermination" &&
                (zm.currentRoom == null || zm.currentNodeIndex < 0 || zm.currentNodeIndex >= zm.nodes.Count ||
                 zm.currentNode.type == WorldNodeType.ExitBoss)) return false;
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
                    if (NetworkedManagerBase<QuestManager>.instance == null || zm.isSidetracking ||
                        zm.currentZone == null || zm.currentZone.useSpecialGeneration || !MapsInitialized()) return false;
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

        private static bool MapsInitialized()
        {
            var actors = NetworkedManagerBase<ActorManager>.instance;
            if (actors == null) return false;
            // Host spawn messages are queued. The Treasure and then its quest must run OnCreate
            // before another map's preflight can see their claimed node. Initialized quests still
            // allow additional maps; this also covers a vanilla shop purchase in the same frame.
            foreach (var identity in Mirror.NetworkServer.spawned.Values)
            {
                if (identity == null || !identity.TryGetComponent<Actor>(out var actor) || !actor.isActive ||
                    actors.allActors.Contains(actor)) continue;
                if (actor is Treasure_TreasureMap || actor is Treasure_TotallyGenuineTreasureMap ||
                    actor is Quest_TreasureMap || actor is Quest_SuspiciousTreasureMap) return false;
            }
            return true;
        }
    }
}
