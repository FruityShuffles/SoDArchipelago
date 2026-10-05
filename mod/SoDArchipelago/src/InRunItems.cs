using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Models;
using UnityEngine;

namespace SoDArchipelago
{
    // #7 shared landing loop. #10–#12 register one handler per kind; true means the vanilla effect has landed.
    // A missing handler or invalid target leaves the item pending. No pending cache belongs to a particular profile.
    public static class InRunItems
    {
        private static readonly Dictionary<string, (Func<GameData.Item, ItemInfo, bool> tryLand,
            Func<GameData.Item, ItemInfo, string> notice)> _handlers =
            new Dictionary<string, (Func<GameData.Item, ItemInfo, bool>, Func<GameData.Item, ItemInfo, string>)>();

        public static bool IsInRunKind(string kind) => kind == "blessing" || kind == "treasure" || kind == "curse";

        public static void Register(string kind, Func<GameData.Item, ItemInfo, bool> tryLand,
            Func<GameData.Item, ItemInfo, string> landingNotice = null)
        {
            if (!IsInRunKind(kind) || tryLand == null) throw new ArgumentException("Invalid in-run handler");
            _handlers[kind] = (tryLand, landingNotice);
        }

        public static void Cleanup() => _handlers.Clear();

        public static void Update()
        {
            if (_handlers.Count == 0 || !Mirror.NetworkServer.active)
                return;
            // instance searches the whole scene (including inactive objects) when a manager is missing.
            // Awake maintains softInstance; polling its cache keeps title/lobby frames free of those searches.
            var gm = NetworkedManagerBase<GameManager>.softInstance;
            if (gm == null || gm.isGameConcluded || !gm.IsLazyCallReady() || !ProfileGuard.Bound || !ApClient.IsConnected)
                return;
            var zm = NetworkedManagerBase<ZoneManager>.softInstance;
            if (zm == null || zm.currentZone == null)
                return;

            var copies = new Dictionary<string, int>();
            // Retry an unavailable item once per update; later kinds can still land.
            var waiting = new HashSet<string>();
            foreach (var info in ApClient.ReceivedItems)
            {
                if (!GameData.ItemsById.TryGetValue(info.ItemId, out var item) || !IsInRunKind(item.Kind)) continue;
                copies.TryGetValue(item.Key, out var count);
                copies[item.Key] = ++count;
                if (count <= ApRecords.GetApplied(false, item.Key) || waiting.Contains(item.Key)) continue;
                if (!_handlers.TryGetValue(item.Kind, out var handler)) continue;
                if (!handler.tryLand(item, info))
                {
                    waiting.Add(item.Key);
                    continue;
                }
                ApRecords.SetApplied(false, item.Key, count);
                DewSave.SaveProfileMain();
                ApClient.Say(handler.notice?.Invoke(item, info) ?? $"Received {item.Name}{ItemHandler.Sender(info)}");
            }
        }
    }
}
