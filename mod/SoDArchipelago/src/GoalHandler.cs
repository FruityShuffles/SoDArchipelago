using System.Collections.Generic;
using System.Linq;

namespace SoDArchipelago
{
    // Goal (DESIGN.md "Goal"): win a run at goal_difficulty or harder with goal_traveler_count different Travelers.
    // A win is either ending (PureWhiteDream, StarlessPath). UnknownFate only exists in demo/booth builds, where it ends
    // the run at the end of the demo's content (PlayGameManager.LoadNextZone), so it never counts.
    // Wins are recorded in the bound profile (Traveler + difficulty id), so they count even when won offline, and the
    // goal is re-checked on every connect.
    public static class GoalHandler
    {
        private static bool _sentThisSession;

        public static void OnGameConcluded(DewGameResult result)
        {
            var local = result?.players?.FirstOrDefault(p => p.isLocalPlayer);
            Log.Info($"Game concluded: result={result?.result} difficulty={result?.difficulty} " +
                     $"({Diagnostics.DifficultyName(result?.difficulty)}) hero={local?.heroType} " +
                     $"visitedWorlds={result?.visitedWorlds}");
            if (!ProfileGuard.Marked || result == null || local == null) return;
            if (result.result != DewGameResult.ResultType.PureWhiteDream &&
                result.result != DewGameResult.ResultType.StarlessPath)
                return;
            if (ApRecords.AddWin(local.heroType, result.difficulty))
            {
                DewSave.SaveProfileMain();
                Log.Info($"Recorded win: {local.heroType} on {result.difficulty}");
            }
            CheckGoal();
        }

        public static void OnLoggedIn()
        {
            _sentThisSession = false;
            CheckGoal();
        }

        public static void CheckGoal()
        {
            if (!ProfileGuard.Bound || _sentThisSession) return;
            int goalRank = ApClient.GetInt("goal_difficulty_rank", 3);
            int goalCount = ApClient.GetInt("goal_traveler_count", 9);
            var travelers = new HashSet<string>(ApRecords.Wins()
                .Where(w => GameData.RankOfGameDifficulty(w.difficultyId) >= goalRank)
                .Select(w => w.traveler));
            Log.Info($"Goal: {travelers.Count}/{goalCount} Travelers have won at rank {goalRank}+ " +
                     $"({string.Join(", ", travelers)})");
            if (travelers.Count < goalCount) return;
            _sentThisSession = true;
            ApClient.Say("Goal complete!");
            ApClient.SendGoal();
        }
    }
}
