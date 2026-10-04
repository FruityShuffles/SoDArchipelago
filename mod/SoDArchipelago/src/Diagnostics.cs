using System;
using System.Linq;
using System.Text;

namespace SoDArchipelago
{
    // [AP] diagnostic log lines (startup data, profile and run state) for bug reports. Everything is wrapped in
    // try/catch: diagnostics must never break the game.
    public static class Diagnostics
    {
        private static readonly string[] DifficultyIds =
            { "diffTutorial", "diffEasy", "diffNormal", "diffHard", "diffNightmare", "diffLimbo" };

        public static string DifficultyName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            try
            {
                return DewLocalization.TryGetUIValue("Difficulty_" + id + "_Name", out var name) ? name : "?";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        public static void LogStartup()
        {
            Try("difficulties", () =>
            {
                // The apworld maps Nap/Deep Sleep/Ominous Dream/Nightmare to diffEasy/diffNormal/diffHard/diffNightmare.
                var sb = new StringBuilder("Difficulty ids -> display names:");
                foreach (var id in DifficultyIds)
                    sb.Append($" {id}='{DifficultyName(id)}' (AP rank {GameData.RankOfGameDifficulty(id)});");
                Log.Info(sb.ToString());
            });
            Try("zones", () =>
            {
                var content = DewBuildProfile.current.content;
                Log.Info($"Build content: zoneCountByTier=[{string.Join(",", content.zoneCountByTier)}] " +
                         $"(worlds per loop = {content.zoneCountByTier.Sum()})");
            });
            Try("star slots", LogStarSlots);
            Try("mastery", LogMastery);
            Try("souvenirs", LogSouvenirs);
        }

        // DESIGN.md "Souvenirs": the shop's pool filter (PropEnt_Merchant_Smoothie.UserCode_TpcPopulateSouvenirs, minus
        // its owned-souvenir check) against game_data.json. A souvenir added by a game update sends nothing until the
        // data is regenerated.
        private static void LogSouvenirs()
        {
            var shop = DewResources.FindAllByNameSubstring<Accessory>("Acc_", ResourceLoadSettings.Light)
                .Where(a => !a.generatedFromServer && !a.excludeFromPool && Dew.IsAccessoryIncludedInGame(a.name))
                .Select(a => a.name).Distinct().OrderBy(n => n).ToList();
            var data = GameData.Souvenirs.Select(l => l.Key).ToList();
            Log.Info($"Shop souvenirs ({shop.Count}): {string.Join(", ", shop)}");
            var missing = shop.Except(data).ToList();
            var extra = data.Except(shop).ToList();
            if (missing.Count > 0 || extra.Count > 0)
                Log.Warn("Shop souvenirs differ from the AP data: not locations " +
                         $"[{string.Join(", ", missing)}], not sold [{string.Join(", ", extra)}]");
        }

        // DESIGN.md "Filler targets": star slot counts live in the hero prefabs (HeroConstellationSettings).
        private static void LogStarSlots()
        {
            int extraSlots = 0, stardust = 0;
            var sb = new StringBuilder("Star slots (default/max) per Traveler:");
            foreach (var type in Dew.allHeroes)
            {
                var hero = DewResources.GetByShortTypeName<Hero>(type.Name, ResourceLoadSettings.Light);
                if (hero == null) continue;
                sb.Append($" {type.Name}[");
                foreach (StarType star in Enum.GetValues(typeof(StarType)))
                {
                    var s = hero.GetConstellationSettings(star);
                    sb.Append($"{star}={s.defaultCount}/{s.maxCount} ");
                    // Extra slot i (0-based past the default count) costs GetRequiredStardustForStarSlotUnlock(i).
                    for (int i = 0; i < s.maxCount - s.defaultCount; i++)
                    {
                        extraSlots++;
                        stardust += Dew.GetRequiredStardustForStarSlotUnlock(i);
                    }
                }
                sb.Append("]");
            }
            Log.Info(sb.ToString());
            Log.Info($"Star slots: {extraSlots} buyable slots cost {stardust} Stardust in total " +
                     "(AP Stardust buffer by default: 54,000 - 45,275 star levels - 3,400 souvenirs = 5,325)");
        }

        // Only for a marked profile: AP code never reads an unbound profile.
        public static void LogMastery()
        {
            if (!ProfileGuard.Marked) return;
            var stats = DewSave.profileStats;
            if (stats?.heroes == null) return;
            Log.Info("Mastery levels: " + string.Join(", ", stats.heroes.Select(h =>
                $"{h.Key}={h.Value?.masteryLevel} (+{h.Value?.currentMasteryPoints} pts)")) +
                $"; total={stats.total?.masteryLevel}; points for level 30->31: " +
                Dew.GetRequiredMasteryPointsToLevelUp(30) + ", 40->41: " + Dew.GetRequiredMasteryPointsToLevelUp(40));
        }

        public static void LogRunStart()
        {
            Try("run start", () =>
            {
                var gsm = NetworkedManagerBase<GameSettingsManager>.instance;
                var gm = NetworkedManagerBase<GameManager>.instance;
                var hero = DewPlayer.local?.hero;
                Log.Info($"Run ready: difficulty={gsm?.difficulty} ({DifficultyName(gsm?.difficulty)}) " +
                         $"bleedOuts={gm?.difficulty?.enableBleedOuts} hero={hero?.GetType().Name} " +
                         $"host={Mirror.NetworkServer.active} marked={ProfileGuard.Marked} bound={ProfileGuard.Bound}");
            });
        }

        public static void Try(string what, Action action)
        {
            try { action(); }
            catch (Exception e) { Log.Warn($"Diagnostics ({what}) failed: {e.Message}"); }
        }
    }
}
