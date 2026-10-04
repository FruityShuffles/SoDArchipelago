using System.Linq;
using Archipelago.MultiClient.Net.Models;
using UnityEngine;

namespace SoDArchipelago
{
    public static class CurseTraps
    {
        public static void Initialize() => InRunItems.Register("curse", TryLand);

        // InRunItems supplies the Bound, connected, ready solo/host run guard.
        private static bool TryLand(GameData.Item item, ItemInfo info)
        {
            var zm = NetworkedManagerBase<ZoneManager>.instance;
            var hero = DewPlayer.local != null ? DewPlayer.local.hero : null;
            if (zm == null || zm.isInRoomTransition || zm.currentRoom == null ||
                zm.currentNodeIndex < 0 || zm.currentNodeIndex >= zm.nodes.Count ||
                zm.currentNode.type == WorldNodeType.ExitBoss ||
                zm.currentRoom.name == "Room_Special_StarlessPath_BossPolaris" ||
                hero.IsNullInactiveDeadOrKnockedOut() ||
                hero.Status.HasStatusEffect<Se_HeroBleedingOut>() ||
                NetworkedManagerBase<QuestManager>.instance == null) return false;

            HatredStrengthType strength;
            switch (item.Target)
            {
                case "Mild": strength = HatredStrengthType.Mild; break;
                case "Potent": strength = HatredStrengthType.Potent; break;
                case "Intense": strength = HatredStrengthType.Powerful; break;
                default: return false;
            }

            // Shrine_Hatred.OnCreate/DoCurse use this same pool and weighting. Do not cache
            // Unity prefabs across resource unloads. AssetRef promotes the discovery prefab to
            // the full gameplay asset, exactly as the shrine's _curses entries do.
            var curses = DewResources.FindAllByType<CurseStatusEffect>(ResourceLoadSettings.Light)
                .Where(c => Dew.IsCurseIncludedInGame(c.GetType().Name))
                .Select(c => new AssetRef<CurseStatusEffect>(c).asset)
                .Where(c => c != null && c.availableStrengths.HasFlag(strength) &&
                    c.IsViable(hero) && c.chanceWeight > 0f).ToList();
            // Vanilla's weighted helper falls back to entry zero when all weights are zero.
            if (curses.Count == 0) return false;
            var prefab = Dew.SelectRandomWeightedInList(curses, c => c.chanceWeight);

            // Mirror only the private DoCurse initialization. The vanilla effect supplies the
            // saved state, networking, notification, quest tracker and lift/knockdown lifecycle.
            var spawned = hero.CreateStatusEffect(prefab, hero, new CastInfo(hero, hero), curse =>
            {
                curse.currentStrength = strength;
                curse.skillLevel = (int)(strength - 1);
                if (Random.value < 0.5f)
                {
                    curse.progressType = QuestProgressType.Kills;
                    curse.requiredAmount = strength == HatredStrengthType.Mild
                        ? 28 + zm.currentZoneIndex * 2
                        : (strength == HatredStrengthType.Potent ? 34 : 50) + zm.currentZoneIndex * 3;
                }
                else
                {
                    curse.progressType = QuestProgressType.Travel;
                    curse.requiredAmount = strength == HatredStrengthType.Mild ? 3 : 4;
                }
            });
            return spawned != null;
        }
    }
}
