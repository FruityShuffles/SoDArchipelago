using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace SoDArchipelago
{
    // Forced Lucid Dreams (DESIGN.md "Forced Lucid Dreams"): the dreams listed in the seed's options stay on in every run
    // until their "Lucid Dream: <name>" item releases them (the normal unlock).
    //  - Lobby, host only (activeLucidDreams is a server setting): every still-forced dream is added back whenever it's
    //    missing, and a start condition refuses to start without them. This is the technique Limbo uses to keep only Evil
    //    dreams on (GameMod_Limbo.EnforceGameRules), turned around.
    //  - Run: world clears and wins only count if every still-forced dream was in effect when the run became ready. That
    //    also covers a player who joined someone else's lobby, where the mod can't force anything.
    public static class ForcedDreams
    {
        private const string LimboDifficulty = "diffLimbo";
        private const string NamePrefix = "Lucid Dream: ";
        private const float LobbyCheckInterval = 0.25f;

        private static readonly Func<string> StartCondition = CheckStartCondition;
        private static PlayLobbyManager _lobby; // the lobby our start condition is registered with
        private static bool _announced;
        private static float _nextLobbyCheck;
        private static List<string> _runMissing; // still-forced dreams not in effect this run; null = not checked yet

        // Login: copy the seed's forced list into the profile, so forcing also works offline. Before items are processed,
        // so a release received in the same login gets its "no longer forced" notice.
        public static void OnLoggedIn()
        {
            if (!ProfileGuard.Bound) return;
            var keys = ApClient.GetStringList("forced_lucid_dreams")
                .Where(k => GameData.ItemsByKey.TryGetValue(k, out var item) && item.Kind == "lucid_dream")
                .ToList();
            if (ApRecords.SetForced(keys)) DewSave.SaveProfileMain();
            var forced = StillForced();
            if (forced.Count > 0) Log.Info($"Forced Lucid Dreams: {Names(forced)} (released: {Names(keys.Except(forced))})");
        }

        // In the seed's forced list and not yet released by its item.
        public static List<string> StillForced()
        {
            if (!ProfileGuard.Marked) return new List<string>();
            var profile = DewSave.profileMain;
            return ApRecords.Forced().Where(k => !ApRecords.HasUnlock(profile, k)).ToList();
        }

        public static string Names(IEnumerable<string> keys) => string.Join(", ", keys.Select(Name));

        private static string Name(string key) =>
            GameData.ItemsByKey.TryGetValue(key, out var item) && item.Name.StartsWith(NamePrefix, StringComparison.Ordinal)
                ? item.Name.Substring(NamePrefix.Length)
                : key;

        // ArchipelagoMod.Update. Not every frame: a quarter second is plenty for a lobby setting.
        public static void UpdateLobby()
        {
            if (Time.unscaledTime < _nextLobbyCheck) return;
            _nextLobbyCheck = Time.unscaledTime + LobbyCheckInterval;

            var lobby = ManagerBase<PlayLobbyManager>.softInstance;
            if (lobby == null)
            {
                _lobby = null;
                _announced = false;
                return;
            }
            var gsm = NetworkedManagerBase<GameSettingsManager>.softInstance;
            if (!NetworkServer.active || !ProfileGuard.Marked || gsm == null || !Applies(gsm)) return;
            if (!ReferenceEquals(lobby, _lobby))
            {
                // A new lobby: the manager and its start conditions are rebuilt for each one.
                _lobby = lobby;
                _announced = false;
                lobby.AddStartGameCondition(StartCondition);
            }
            if (gsm.state != GameState.InLobby) return;

            var forced = StillForced();
            if (forced.Count == 0) return;
            var missing = forced.Where(k => !gsm.activeLucidDreams.Contains(k)).ToList();
            foreach (var key in missing) gsm.AddLucidDream(key);
            if (!_announced)
            {
                _announced = true;
                ApClient.Say($"Archipelago forces these Lucid Dreams on until you receive them: {Names(forced)}");
            }
            else if (missing.Count > 0)
            {
                Log.Info($"Forced Lucid Dreams turned back on: {Names(missing)}");
            }
        }

        // Not in Limbo lobbies (Limbo removes non-Evil dreams itself, and Limbo runs send no checks), and not when
        // continuing a saved run (its dreams come from the save; the run check below still applies).
        private static bool Applies(GameSettingsManager gsm) =>
            DewNetworkManager.startSettings?.continueData == null && gsm.difficulty != LimboDifficulty;

        private static string CheckStartCondition()
        {
            try
            {
                var gsm = NetworkedManagerBase<GameSettingsManager>.softInstance;
                if (!NetworkServer.active || !ProfileGuard.Marked || gsm == null || !Applies(gsm)) return null;
                var missing = StillForced().Where(k => !gsm.activeLucidDreams.Contains(k)).ToList();
                return missing.Count == 0 ? null : "Forced by Archipelago: " + Names(missing);
            }
            catch (Exception e)
            {
                Log.Warn("Forced Lucid Dream start check failed: " + e.Message);
                return null;
            }
        }

        // Mod unload.
        public static void Cleanup()
        {
            if (_lobby != null) _lobby.RemoveStartGameCondition(StartCondition);
            _lobby = null;
        }

        // A GameManager started: a new (or continued) run, not checked yet.
        public static void OnRunStarting() => _runMissing = null;

        // GameManager.CallOnReady.
        public static void OnRunReady()
        {
            _runMissing = null;
            Diagnostics.Try("forced dreams", () => CheckRun());
        }

        // Whether this run's world clears and wins count: every still-forced dream is in effect.
        public static bool RunCounts(string what)
        {
            var missing = CheckRun();
            if (missing.Count == 0) return true;
            Log.Info($"Not counted ({what}): forced Lucid Dreams missing this run: {Names(missing)}");
            return false;
        }

        // Dreams in effect: the LucidDream actors (what the game actually created, also restored when continuing a run),
        // plus the synced lobby setting.
        private static List<string> CheckRun()
        {
            if (_runMissing != null) return _runMissing;
            var forced = StillForced();
            var active = new HashSet<string>();
            var gsm = NetworkedManagerBase<GameSettingsManager>.instance;
            if (gsm != null) active.UnionWith(gsm.activeLucidDreams);
            var actors = NetworkedManagerBase<ActorManager>.instance;
            if (actors != null)
                foreach (var actor in actors.allActors)
                    if (actor is LucidDream) active.Add(actor.GetType().Name);
            _runMissing = forced.Where(k => !active.Contains(k)).ToList();
            if (forced.Count > 0)
                Log.Info($"Forced Lucid Dreams this run: {Names(forced)}; in effect: {string.Join(", ", active)}; " +
                         $"missing: {(_runMissing.Count == 0 ? "none" : Names(_runMissing))}");
            if (_runMissing.Count > 0)
                ApClient.Say($"Forced Lucid Dreams missing ({Names(_runMissing)}): world clears and wins won't count " +
                             "this run.");
            return _runMissing;
        }
    }
}
