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
    // Connection flow: ConnectAsync -> check RoomInfo.SeedName against the loaded profile's marker. A profile bound to
    // this seed/slot logs in right away. An unmarked profile waits for ap_bind, then logs in *provisionally*: nothing acts
    // on the profile until it is Bound, and the marker is written only after the login and the slot_data checks succeed,
    // so a wrong slot name or password never marks a profile. A profile bound to anything else never logs in.
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

        // One session and its socket. Closing a socket that is still connecting fails, and MultiClient.Net's own connect
        // timeout abandons the attempt without cancelling the underlying socket connect, so a closed connection can still
        // open later. Close() therefore records the request, and the socket's SocketOpened event closes it again whenever
        // it opens after that. Closing an already closed socket is harmless.
        private sealed class Connection
        {
            public readonly ArchipelagoSession Session;
            private volatile bool _closeRequested;

            public Connection(ArchipelagoSession session)
            {
                Session = session;
                session.Socket.SocketOpened += () =>
                {
                    if (_closeRequested) Close();
                };
            }

            public void Close()
            {
                _closeRequested = true;
                var session = Session;
                Task.Run(async () =>
                {
                    try { await session.Socket.DisconnectAsync(); }
                    catch (Exception) { /* never connected, or already closed */ }
                });
            }
        }

        // ap_bind / ap_bind_force: bind this marker after the login succeeds, if nothing changed in between.
        private sealed class BindRequest
        {
            public int Attempt;
            public int Generation;
            public string Marker;
            public bool Force;
        }

        private static readonly ConcurrentQueue<Work> _queue = new ConcurrentQueue<Work>();
        private static volatile int _attempt;
        private static Connection _conn; // main thread only
        private static DeathLinkService _deathLink;
        private static string _password;
        private static string _pendingMarker;
        private static BindRequest _bindRequest;

        public static IReadOnlyList<ItemInfo> ReceivedItems =>
            (IReadOnlyList<ItemInfo>)_conn?.Session.Items.AllItemsReceived ?? Array.Empty<ItemInfo>();

        public static void Connect(string server, string slot, string password)
        {
            Disconnect(null);
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(slot))
            {
                Say("Set the server and slot first (mod config, or ap_server / ap_slot).");
                return;
            }
            if (DewSave.profileMainPath == null || ProfileGuard.LoadFailed)
            {
                Say("The transient profile (or a profile that failed to load) can't be used. Load a profile first.");
                return;
            }

            ArchipelagoSession session;
            try
            {
                session = ArchipelagoSessionFactory.CreateSession(server.Trim());
            }
            catch (Exception e)
            {
                Say("Couldn't connect: " + Describe(e));
                return;
            }

            // Tracked from the start, so Disconnect (another ap_connect, a profile switch, unloading) can close a
            // connection that is still being made.
            int attempt = ++_attempt;
            var conn = _conn = new Connection(session);
            Status = State.Connecting;
            SlotName = slot.Trim();
            _password = string.IsNullOrEmpty(password) ? null : password;
            Say($"Connecting to {server} as {SlotName}...");

            session.Socket.SocketClosed += reason => Enqueue(attempt, () => Disconnect("Connection closed: " + reason));
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

            Task.Run(async () =>
            {
                try
                {
                    var roomInfo = await session.ConnectAsync();
                    if (attempt != _attempt)
                    {
                        conn.Close(); // abandoned while connecting
                        return;
                    }
                    Enqueue(attempt, () => OnRoomInfo(attempt, roomInfo.SeedName));
                }
                catch (Exception e)
                {
                    if (attempt != _attempt) conn.Close();
                    else Enqueue(attempt, () => Disconnect("Couldn't connect: " + Describe(e)));
                }
            });
        }

        // DESIGN.md step 5: the seed check happens here, before logging in.
        private static void OnRoomInfo(int attempt, string seed)
        {
            Seed = seed;
            var marker = ProfileGuard.Marker(seed, SlotName);
            var current = ProfileGuard.MarkerOf(DewSave.profileMain);
            Log.Info($"Room seed {seed}; wanted marker {marker}; loaded profile '{DewSave.profileMain.name}' marker " +
                     (current ?? "<none>"));

            if (ProfileGuard.LoadFailed || DewSave.profileMainPath == null)
            {
                Disconnect("The loaded profile can't be used (it failed to load, or is the transient profile).");
                return;
            }
            if (current == marker)
            {
                Login(attempt);
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
                Say($"Profile '{DewSave.profileMain.name}' isn't bound yet. Type ap_bind to log in and bind it to seed " +
                    $"{seed}, slot {SlotName}. (Binding is permanent; it only happens if the login succeeds.)");
            else
                Say($"Profile '{DewSave.profileMain.name}' isn't fresh ({details}), so it can't be bound. Create a " +
                    "new profile for this seed. To bind it anyway (recovery only), type ap_bind_force.");
        }

        public static void ConfirmBind(bool force)
        {
            if (Status != State.AwaitingBind || _pendingMarker == null)
            {
                Say(Status == State.LoggingIn ? "Already logging in." : "Nothing to bind. Type ap_connect first.");
                return;
            }
            if (!CanBindNow(out var why))
            {
                Say(why);
                return;
            }
            if (!force && !ProfileGuard.IsFresh(out var details))
            {
                Say($"Profile isn't fresh ({details}). Use ap_bind_force to bind it anyway.");
                return;
            }
            _bindRequest = new BindRequest
            {
                Attempt = _attempt,
                Generation = ProfileGuard.Generation,
                Marker = _pendingMarker,
                Force = force,
            };
            Say("Logging in; the profile is bound once the login succeeds.");
            Login(_attempt);
        }

        // DESIGN.md step 3: bind only from the title screen, never in a lobby or a run: the lobby's Travelers and the run's
        // loot pool were built from the unbound (vanilla) unlocks.
        private static bool CanBindNow(out string why)
        {
            bool client = Mirror.NetworkClient.active, server = Mirror.NetworkServer.active;
            bool lobby = ManagerBase<LobbyManager>.softInstance != null;
            bool run = NetworkedManagerBase<GameManager>.instance != null;
            if (!client && !server && !lobby && !run)
            {
                why = null;
                return true;
            }
            why = "Binding only works on the title screen, not in a lobby or a run.";
            Log.Info($"Bind refused: networkClient={client} networkServer={server} lobby={lobby} run={run}");
            return false;
        }

        private static void Login(int attempt)
        {
            Status = State.LoggingIn;
            var session = _conn.Session;
            var slot = SlotName;
            var password = _password;
            Task.Run(async () =>
            {
                try
                {
                    var result = await session.LoginAsync(GameData.GameName, slot, ItemsHandlingFlags.AllItems,
                        password: password, requestSlotData: true);
                    if (result is LoginSuccessful ok)
                        Enqueue(attempt, () => OnLoggedIn(ok));
                    else
                    {
                        var errors = string.Join("; ", ((LoginFailure)result).Errors);
                        Enqueue(attempt, () => Disconnect("Login failed: " + errors + NothingBound()));
                    }
                }
                catch (Exception e)
                {
                    Enqueue(attempt, () => Disconnect("Login failed: " + Describe(e) + NothingBound()));
                }
            });
        }

        private static string NothingBound() => _bindRequest != null ? " Nothing was bound." : "";

        private static void OnLoggedIn(LoginSuccessful ok)
        {
            var marker = ProfileGuard.Marker(Seed, SlotName);
            var slotData = ok.SlotData ?? new Dictionary<string, object>();

            // Same data as this mod? Checked before anything is bound.
            int version = GetInt(slotData, "data_format_version", -1);
            string hash = slotData.TryGetValue("data_hash", out var h) ? h as string : null;
            if (version != GameData.DataFormatVersion || hash != GameData.DataHash)
            {
                Disconnect($"This seed was generated with different game data (apworld data version {version}, hash " +
                           $"{Short(hash)}) than this mod has (version {GameData.DataFormatVersion}, hash " +
                           $"{Short(GameData.DataHash)}). Use matching versions of the apworld and the mod." +
                           NothingBound());
                return;
            }

            // The profile may have changed since the seed check. Enqueue's generation tag drops that case, but check the
            // profile once more before this session can act on it.
            if (ProfileGuard.LoadFailed || DewSave.profileMainPath == null)
            {
                Disconnect("The loaded profile changed while logging in." + NothingBound());
                return;
            }
            var current = ProfileGuard.MarkerOf(DewSave.profileMain);
            if (current == null && !BindAfterLogin(marker))
                return;
            if (ProfileGuard.MarkerOf(DewSave.profileMain) != marker)
            {
                Disconnect("The loaded profile changed while logging in.");
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
                _deathLink = _conn.Session.CreateDeathLinkService();
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

        // The provisional login succeeded: write the marker if the ap_bind request still matches what is loaded.
        private static bool BindAfterLogin(string marker)
        {
            var request = _bindRequest;
            _bindRequest = null;
            if (request == null || request.Attempt != _attempt || request.Generation != ProfileGuard.Generation ||
                request.Marker != marker)
            {
                Disconnect("The loaded profile changed while logging in. Nothing was bound.");
                return false;
            }
            if (!CanBindNow(out var why))
            {
                Disconnect(why + " Nothing was bound.");
                return false;
            }
            if (!request.Force && !ProfileGuard.IsFresh(out var details))
            {
                Disconnect($"The profile isn't fresh any more ({details}). Nothing was bound.");
                return false;
            }
            if (request.Force) Log.Info("Recovery override: binding without the fresh-profile check.");
            if (!ProfileGuard.Bind(marker))
            {
                Disconnect("Binding failed.");
                return false;
            }
            return true;
        }

        public static void Disconnect(string reason)
        {
            _attempt++;
            var conn = _conn;
            _conn = null;
            _deathLink = null;
            _pendingMarker = null;
            _bindRequest = null;
            SlotData = null;
            ProfileGuard.SetSessionMarker(null);
            var wasActive = Status != State.Disconnected;
            Status = State.Disconnected;
            if (reason != null && wasActive) Say(reason);
            conn?.Close();
        }

        public static void SendLocations(ICollection<long> ids)
        {
            var session = _conn?.Session;
            if (!ProfileGuard.Bound || session == null || ids.Count == 0) return;
            var array = ids.ToArray();
            Task.Run(async () =>
            {
                try { await session.Locations.CompleteLocationChecksAsync(array); }
                catch (Exception e) { Debug.LogWarning("[AP] Sending checks failed (resent on reconnect): " + e.Message); }
            });
        }

        public static void SendGoal()
        {
            var session = _conn?.Session;
            if (!ProfileGuard.Bound || session == null) return;
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

        private static string Short(string hash) =>
            string.IsNullOrEmpty(hash) ? "<none>" : hash.Substring(0, Math.Min(12, hash.Length));

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

        // Called when a profile is loaded, created or converted (or a load failed).
        public static void OnProfileChanged()
        {
            if (Status == State.Disconnected) return;
            if (ProfileGuard.LoadFailed)
            {
                Disconnect("The profile failed to load, so Archipelago disconnected.");
                return;
            }
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
