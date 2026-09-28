using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SoDArchipelago
{
    // Shuffled star requirements (DESIGN.md "Shuffled star requirements"). A star's mastery requirement is the
    // StarEffect.requiredLevel field of its prefab, and only the lobby's constellation screens read it (the buy check, the
    // locked look, the number on the icon, the list order). The game unloads and reloads prefabs
    // (DewResources.UnloadUnused), so instead of changing each prefab once, every star DewResources hands out gets the
    // seed's level (StarPatches), and the stars already handed out are updated whenever the level list changes.
    // Local only: nothing here is synced to other players.
    public static class StarRequirements
    {
        // The seed's levels by star key, from the profile's AP records. Empty = vanilla (unmarked profile, option off,
        // or a seed from before the option).
        private static Dictionary<string, int> _levels = new Dictionary<string, int>();

        // Every star instance handed out since the mod loaded, by instance ID, with its vanilla level.
        private static readonly Dictionary<int, (StarEffect star, int vanilla)> _seen =
            new Dictionary<int, (StarEffect, int)>();

        private static HashSet<string> _guids; // prefab guids (heavy and light) of every star
        private static bool _buildingGuids;

        // Login: copy the seed's levels into the profile, so they also apply offline.
        public static void OnLoggedIn()
        {
            if (!ProfileGuard.Bound) return;
            var levels = ApClient.GetIntMap("star_requirements");
            var known = new HashSet<string>(Dew.allStarTypes.Select(t => t.Name));
            var unknown = levels.Keys.Where(k => !known.Contains(k)).ToList();
            if (unknown.Count > 0)
            {
                Log.Warn($"slot_data lists stars this game doesn't have (skipped): {string.Join(", ", unknown)}");
                foreach (var key in unknown) levels.Remove(key);
            }
            if (ApRecords.SetStars(levels)) DewSave.SaveProfileMain();
            Refresh();
        }

        // Mod load, profile change and login: reload the levels and update every star handed out so far.
        public static void Refresh()
        {
            var levels = ProfileGuard.Marked ? ApRecords.Stars() : new Dictionary<string, int>();
            bool changed = levels.Count != _levels.Count ||
                           levels.Any(kv => !_levels.TryGetValue(kv.Key, out var old) || old != kv.Value);
            _levels = levels;
            foreach (var id in _seen.Keys.ToList())
            {
                var (star, vanilla) = _seen[id];
                if (star == null) _seen.Remove(id); // unloaded
                else star.requiredLevel = Level(star, vanilla);
            }
            if (!changed) return;
            int moved = _seen.Values.Count(e => e.star != null && e.star.requiredLevel != e.vanilla);
            Log.Info(_levels.Count == 0
                ? "Star requirements: vanilla"
                : $"Star requirements: shuffled for {_levels.Count} stars ({moved} loaded stars changed so far)");
        }

        // Mod unload: put back the vanilla levels.
        public static void Cleanup()
        {
            foreach (var (star, vanilla) in _seen.Values)
                if (star != null) star.requiredLevel = vanilla;
            _seen.Clear();
            _levels = new Dictionary<string, int>();
        }

        // StarPatches: a star prefab (or its light version) was handed out.
        public static void Apply(StarEffect star)
        {
            int id = star.GetInstanceID();
            if (!_seen.TryGetValue(id, out var entry))
            {
                entry = (star, star.requiredLevel);
                _seen[id] = entry;
            }
            star.requiredLevel = Level(star, entry.vanilla);
        }

        private static int Level(StarEffect star, int vanilla) =>
            _levels.TryGetValue(star.GetType().Name, out var level) ? level : vanilla;

        public static bool IsStarGuid(string guid)
        {
            if (guid == null || _buildingGuids) return false; // building the set must not recurse through Load
            if (_guids == null)
            {
                _buildingGuids = true;
                try
                {
                    var guids = new HashSet<string>();
                    var db = DewResources.database;
                    foreach (var type in Dew.allStarTypes)
                    {
                        if (!db.typeToGuid.TryGetValue(type, out var heavy)) continue;
                        guids.Add(heavy);
                        if (db.heavyToLightGuidMap.TryGetValue(heavy, out var light)) guids.Add(light);
                    }
                    _guids = guids;
                }
                finally
                {
                    _buildingGuids = false;
                }
            }
            return _guids.Contains(guid);
        }

        public static StarEffect StarOf(UnityEngine.Object obj) =>
            obj is GameObject go ? go.GetComponent<StarEffect>() : (obj as Component)?.GetComponent<StarEffect>();
    }
}
