using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using UnityEngine;

namespace SoDArchipelago
{
    // Owns the Archipelago session. Socket callbacks arrive on background threads and never touch Unity or game state:
    // they queue work that Pump() runs on the main thread. Each queued item is tagged with the connection attempt and
    // the profile generation, and is dropped if either changed before it ran (DESIGN.md "Save profile binding" step 6).
    //
    // Connection flow: ConnectAsync -> check RoomInfo.SeedName against the loaded profile's marker -> (first time:
    // wait for ap_bind) -> LoginAsync. The mod never logs in with a profile that isn't bound to that seed/slot.
    public static class ApClient
    {
        public enum State { Disconnected, Connecting, AwaitingBind, LoggingIn, Connected }

        public static State Status { get; private set; } = State.Disconnected;
        public static string Seed { get; private set; }
        public static string SlotName { get; private set; }
        public static Dictionary<string, object> SlotData { get; private set; }

        // All raised on the main thread.
        public static event Action LoggedIn;
        public static event Action ItemsChanged;
        public static event Action<string, string> DeathLinkReceived; // source, cause
        public static event Action<string> Notice; // text for the on-screen feed

        private struct Work
        {
            public int Attempt;
            public int Generation;
            public Action Action;
        }

        private static readonly ConcurrentQueue<Work> _queue = new ConcurrentQueue<Work>();
        private static volatile int _attempt;
        private static ArchipelagoSession _session;
        private static DeathLinkService _deathLink;
        private static string _password;
        private static string _pendingMarker;

        public static IReadOnlyList<ItemInfo> ReceivedItems =>
            (IReadOnlyList<ItemInfo>)_session?.Items.AllItemsReceived ?? Array.Empty<ItemInfo>();

        public static void Connect(string server, string slot, string password)
        {
            Disconnect(null);
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(slot))
            {
                Say("Set the server and slot first (mod config, or ap_server / ap_slot).");
                return;
            }
            if (DewSave.profileMainPath == null)
            {
                Say("The transient profile can't be used. Create or load a profile first.");
                return;
            }

            int attempt = ++_attempt;
            Status = State.Connecting;
            SlotName = slot.Trim();
            _password = string.IsNullOrEmpty(password) ? null : password;
            Say($"Connecting to {server} as {SlotName}...");

            Task.Run(async () =>
            {
                try
                {
                    var session = ArchipelagoSessionFactory.CreateSession(server.Trim());
                    session.Socket.SocketClosed += reason =>
                        Enqueue(attempt, () => Disconnect("Connection closed: " + reason));
                    session.Items.ItemReceived += _ => Enqueue(attempt, () => ItemsChanged?.Invoke());
                    session.MessageLog.OnMessageReceived += message =>
                    {
                        // Items we receive get their own notification from ItemHandler; skip other players' traffic.
                        if (message is ItemSendLogMessage send &&
                            (send.IsReceiverTheActivePlayer || !send.IsRelatedToActivePlayer))
                            return;
                        var text = message.ToString();
                        Enqueue(attempt, () => Notice?.Invoke(text));
                    };
                    var roomInfo = await session.ConnectAsync();
                    Enqueue(attempt, () => OnRoomInfo(attempt, session, roomInfo.SeedName));
                }
                catch (Exception e)
                {
                    Enqueue(attempt, () => Disconnect("Couldn't connect: " + Describe(e)));
                }
            });
        }

        // DESIGN.md step 5: the seed check happens here, before logging in.
        private static void OnRoomInfo(int attempt, ArchipelagoSession session, string seed)
        {
            _session = session;
            Seed = seed;
            var marker = ProfileGuard.Marker(seed, SlotName);
            var current = ProfileGuard.MarkerOf(DewSave.profileMain);
            Log.Info($"Room seed {seed}; wanted marker {marker}; loaded profile '{DewSave.profileMain.name}' marker " +
                     (current ?? "<none>"));

            if (current == marker)
            {
                Login(attempt, marker);
                return;
            }
            if (current != null)
            {
                Disconnect($"The loaded profile '{DewSave.profileMain.name}' belongs to a different seed/slot " +
                           $"({current.Substring(ProfileGuard.MarkerPrefix.Length)}). Load the profile for seed " +
                           $"{seed}, slot {SlotName}, or create a new profile for it.");
                return;
            }

            _pendingMarker = marker;
            Status = State.AwaitingBind;
            bool fresh = ProfileGuard.IsFresh(out var details);
            Log.Info("Fresh-profile check: " + details);
            if (fresh)
                Say($"Profile '{DewSave.profileMain.name}' isn't bound yet. Type ap_bind to bind it to seed {seed}, " +
                    $"slot {SlotName}. (Binding is permanent.)");
            else
                Say($"Profile '{DewSave.profileMain.name}' isn't fresh ({details}), so it can't be bound. Create a " +
                    "new profile for this seed. To bind it anyway (recovery only), type ap_bind_force.");
        }

        public static void ConfirmBind(bool force)
        {
            if (Status != State.AwaitingBind || _pendingMarker == null)
            {
                Say("Nothing to bind. Type ap_connect first.");
                return;
            }
            if (!force && !ProfileGuard.IsFresh(out var details))
            {
                Say($"Profile isn't fresh ({details}). Use ap_bind_force to bind it anyway.");
                return;
            }
            if (force) Log.Info("Recovery override: binding without the fresh-profile check.");
            if (!ProfileGuard.Bind(_pendingMarker))
            {
                Disconnect("Binding failed.");
                return;
            }
            Login(_attempt, _pendingMarker);
        }

        private static void Login(int attempt, string marker)
        {
            Status = State.LoggingIn;
            var session = _session;
            var slot = SlotName;
            var password = _password;
            Task.Run(async () =>
            {
                try
                {
                    var result = await session.LoginAsync(GameData.GameName, slot, ItemsHandlingFlags.AllItems,
                        password: password, requestSlotData: true);
                    if (result is LoginSuccessful ok)
                        Enqueue(attempt, () => OnLoggedIn(marker, ok));
                    else
                    {
                        var errors = string.Join("; ", ((LoginFailure)result).Errors);
                        Enqueue(attempt, () => Disconnect("Login failed: " + errors));
                    }
                }
                catch (Exception e)
                {
                    Enqueue(attempt, () => Disconnect("Login failed: " + Describe(e)));
                }
            });
        }

        private static void OnLoggedIn(string marker, LoginSuccessful ok)
        {
            // The profile may have changed between the seed check and now; Enqueue's generation tag drops that case,
            // but check the marker once more before this session can act on the profile.
            if (ProfileGuard.MarkerOf(DewSave.profileMain) != marker || DewSave.profileMainPath == null)
            {
                Disconnect("The loaded profile changed while logging in.");
                return;
            }
            var slotData = ok.SlotData ?? new Dictionary<string, object>();
            int version = GetInt(slotData, "data_format_version", -1);
            if (version != GameData.DataFormatVersion)
            {
                Disconnect($"This seed was generated with apworld data version {version}, but this mod expects " +
                           $"{GameData.DataFormatVersion}. Use matching versions of the apworld and the mod.");
                return;
            }

            SlotData = slotData;
            _pendingMarker = null;
            ProfileGuard.SetSessionMarker(marker);
            Status = State.Connected;
            Say($"Connected: seed {Seed}, slot {SlotName} (profile '{DewSave.profileMain.name}').");
            Log.Info("slot_data: " + string.Join(", ", slotData.Select(kv => kv.Key + "=" + kv.Value)));

            if (GetBool("death_link"))
            {
                _deathLink = _session.CreateDeathLinkService();
                int attempt = _attempt;
                _deathLink.OnDeathLinkReceived += dl =>
                {
                    var source = dl.Source;
                    var cause = dl.Cause;
                    Enqueue(attempt, () => DeathLinkReceived?.Invoke(source, cause));
                };
                _deathLink.EnableDeathLink();
                Log.Info("DeathLink enabled.");
            }

            LoggedIn?.Invoke();
        }

        public static void Disconnect(string reason)
        {
            _attempt++;
            var session = _session;
            _session = null;
            _deathLink = null;
            _pendingMarker = null;
            SlotData = null;
            ProfileGuard.SetSessionMarker(null);
            var wasActive = Status != State.Disconnected;
            Status = State.Disconnected;
            if (reason != null && wasActive) Say(reason);
            if (session == null) return;
            Task.Run(async () =>
            {
                try { await session.Socket.DisconnectAsync(); }
                catch (Exception) { /* already closed */ }
            });
        }

        public static void SendLocations(ICollection<long> ids)
        {
            if (!ProfileGuard.Bound || _session == null || ids.Count == 0) return;
            var session = _session;
            var array = ids.ToArray();
            Task.Run(async () =>
            {
                try { await session.Locations.CompleteLocationChecksAsync(array); }
                catch (Exception e) { Debug.LogWarning("[AP] Sending checks failed (resent on reconnect): " + e.Message); }
            });
        }

        public static void SendGoal()
        {
            if (!ProfileGuard.Bound || _session == null) return;
            var session = _session;
            Task.Run(() =>
            {
                try { session.SetGoalAchieved(); }
                catch (Exception e) { Debug.LogWarning("[AP] Sending goal failed (resent on reconnect): " + e.Message); }
            });
        }

        public static bool DeathLinkEnabled => _deathLink != null && ProfileGuard.Bound;

        public static void SendDeathLink(string cause)
        {
            if (!DeathLinkEnabled) return;
            var service = _deathLink;
            var link = new DeathLink(SlotName, cause);
            Task.Run(() =>
            {
                try { service.SendDeathLink(link); }
                catch (Exception e) { Debug.LogWarning("[AP] Sending DeathLink failed: " + e.Message); }
            });
        }

        public static string PlayerName(ItemInfo item)
        {
            try { return item.Player?.Name ?? "?"; }
            catch (Exception) { return "?"; }
        }

        public static int GetInt(string key, int fallback) => GetInt(SlotData, key, fallback);

        public static bool GetBool(string key) => SlotData != null && SlotData.TryGetValue(key, out var v) && v != null &&
                                                  Convert.ToBoolean(v);

        private static int GetInt(Dictionary<string, object> data, string key, int fallback)
        {
            if (data == null || !data.TryGetValue(key, out var v) || v == null) return fallback;
            try { return Convert.ToInt32(v); }
            catch (Exception) { return fallback; }
        }

        private static void Enqueue(int attempt, Action action) =>
            _queue.Enqueue(new Work { Attempt = attempt, Generation = ProfileGuard.Generation, Action = action });

        // Drain queued work on the main thread. Called from ArchipelagoMod.Update.
        public static void Pump()
        {
            while (_queue.TryDequeue(out var work))
            {
                if (work.Attempt != _attempt || work.Generation != ProfileGuard.Generation) continue;
                try { work.Action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // Called when a different profile is loaded or created.
        public static void OnProfileChanged()
        {
            if (Status == State.Disconnected) return;
            var marker = ProfileGuard.MarkerOf(DewSave.profileMain);
            var sessionMarker = ProfileGuard.SessionMarker ?? _pendingMarker;
            if (DewSave.profileMainPath != null && marker != null && marker == sessionMarker && Status == State.Connected)
            {
                // Same bound profile reloaded: queued work was dropped, so rebuild everything.
                Log.Info("Bound profile reloaded; rebuilding checks and items.");
                LoggedIn?.Invoke();
                return;
            }
            Disconnect("Profile switched, so Archipelago disconnected. Type ap_connect again.");
        }

        public static void Say(string text)
        {
            Log.Info(text);
            Notice?.Invoke(text);
        }

        private static string Describe(Exception e)
        {
            while (e is AggregateException && e.InnerException != null) e = e.InnerException;
            return e is TaskCanceledException ? "timed out" : e.Message;
        }
    }
}
