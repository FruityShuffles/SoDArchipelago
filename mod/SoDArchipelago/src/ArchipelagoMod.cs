using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SoDArchipelago
{
    // Entry point. The game's mod loader adds every ModBehaviour in our assemblies to an inactive container GameObject,
    // sets `instance`, loads the config, registers [ConsoleCommand] methods, then activates it (Awake runs). Mods can be
    // unloaded and live-reloaded, so OnDestroy undoes everything.
    public class ArchipelagoMod : ModBehaviour
    {
        public ApConfig config = new ApConfig();

        private const int FeedSize = 8;
        private const float FeedSeconds = 20f;
        private readonly List<(string text, float time)> _feed = new List<(string, float)>();
        private bool _itemsDirty;
        private GUIStyle _offlineStyle;

        private void Awake()
        {
            // MultiClient.Net is merged into this DLL (see the csproj), but it was built against Newtonsoft.Json 11. Register
            // the fallback before any method that touches it is compiled.
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            Initialize();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize()
        {
            instance.isAlteringGameplay = true;
            GameData.Load();
            MapBlessings.Initialize();
            Log.Info($"Loaded {mod.metadata.id} {mod.metadata.modVer}; data format {GameData.DataFormatVersion}, " +
                     $"extracted from game {GameData.ExtractedFromGameVersion}; running game {Application.version}");

            ApClient.Notice += OnNotice;
            ApClient.LoggedIn += OnLoggedIn;
            ApClient.ItemsChanged += OnItemsChanged;
            ApClient.DeathLinkReceived += DeathLinkHandler.OnDeathLinkReceived;

            CallOnNetworkedManager<ZoneManager>(zm =>
            {
                Action<EventInfoLoadZone> onZone = CheckHandler.OnZoneLoaded;
                zm.ClientEvent_OnZoneLoaded += onZone;
                return () => zm.ClientEvent_OnZoneLoaded -= onZone;
            });
            CallOnNetworkedManager<GameResultManager>(grm =>
            {
                Action<DewGameResult> onConcluded = GoalHandler.OnGameConcluded;
                grm.ClientEvent_OnGameConcluded += onConcluded;
                return () => grm.ClientEvent_OnGameConcluded -= onConcluded;
            });
            CallOnNetworkedManager<QuestManager>(qm =>
            {
                Action<DewQuest> onRemoved = CheckHandler.OnQuestRemoved;
                qm.ClientEvent_OnQuestRemoved += onRemoved;
                return () => qm.ClientEvent_OnQuestRemoved -= onRemoved;
            });
            CallOnNetworkedManager<ClientEventManager>(cem =>
            {
                Action<Hero> onKnockedOut = DeathLinkHandler.OnHeroKnockedOut;
                Action<Hero> onRevive = DeathLinkHandler.OnHeroRevive;
                cem.OnHeroKnockedOut += onKnockedOut;
                cem.OnHeroRevive += onRevive;
                return () =>
                {
                    cem.OnHeroKnockedOut -= onKnockedOut;
                    cem.OnHeroRevive -= onRevive;
                };
            });
            CallOnNetworkedManager<GameManager>(gm =>
            {
                DeathLinkHandler.Reset();
                ForcedDreams.OnRunStarting();
                GameManager.CallOnReady(Diagnostics.LogRunStart);
                GameManager.CallOnReady(ForcedDreams.OnRunReady);
                return DeathLinkHandler.Reset;
            });

            harmony.PatchAll(typeof(ArchipelagoMod).Assembly);
            Diagnostics.LogStartup();

            // The profile was loaded (and validated) at game start, before this mod was loaded, so check that load and
            // enforce the AP unlock state now.
            ProfileGuard.CheckStartupLoad();
            Log.Info($"Loaded profile '{DewSave.profileMain?.name}' path={DewSave.profileMainPath ?? "<transient>"} " +
                     $"marker={ProfileGuard.MarkerOf(DewSave.profileMain) ?? "<none>"}");
            if (ProfileGuard.Marked && UnlockState.Enforce(DewSave.profileMain, "mod load") > 0)
                DewSave.SaveProfileMain();
            StarRequirements.Refresh();
        }

        private void Update()
        {
            ApClient.Pump();
            ForcedDreams.UpdateLobby();
            if (_itemsDirty)
            {
                _itemsDirty = false;
                ItemHandler.ProcessAll("items received");
            }
            InRunItems.Update();
        }

        private void OnDestroy()
        {
            ApClient.Notice -= OnNotice;
            ApClient.LoggedIn -= OnLoggedIn;
            ApClient.ItemsChanged -= OnItemsChanged;
            ApClient.DeathLinkReceived -= DeathLinkHandler.OnDeathLinkReceived;
            ApClient.Disconnect(null);
            InRunItems.Cleanup();
            WareShopUi.Cleanup();
            ForcedDreams.Cleanup();
            harmony.UnpatchAll(harmony.Id);
            StarRequirements.Cleanup();
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
            Log.Info("Unloaded");
        }

        // ProfilePatches: a profile was loaded (ok = LoadProfile's result), created or converted.
        internal static void OnProfileLoaded(string how, bool ok)
        {
            try
            {
                ProfileGuard.OnProfileLoaded(how, ok);
                StarRequirements.Refresh();
                ApClient.OnProfileChanged();
                if (ProfileGuard.Marked && !ProfileGuard.Bound)
                    ApClient.Say("Archipelago profile loaded (offline). Type ap_connect to connect.");
            }
            catch (Exception e)
            {
                // Never let this escape into the game's profile code.
                Debug.LogException(e);
            }
        }

        private static void OnLoggedIn()
        {
            if (UnlockState.Enforce(DewSave.profileMain, "connect") > 0) DewSave.SaveProfileMain();
            ForcedDreams.OnLoggedIn();
            StarRequirements.OnLoggedIn();
            PassiveMastery.OnLoggedIn();
            JonasWares.OnLoggedIn();
            CheckHandler.ResendAll();
            ItemHandler.ProcessAll("connect");
            Log.Info("Unlocked Travelers: " + string.Join(", ",
                Dew.HeroOrder.Where(h => !UnlockState.IsHeroLocked(DewSave.profileMain, h))));
            GoalHandler.OnLoggedIn();
        }

        private void OnItemsChanged() => _itemsDirty = true;

        private void OnNotice(string text)
        {
            _feed.Add((text, Time.unscaledTime));
            if (_feed.Count > FeedSize) _feed.RemoveAt(0);
        }

        // The game ships Newtonsoft.Json 13 while the merged MultiClient.Net code asks for 11. Mono already binds that on
        // its own when the loader checks our types; this is a fallback that points it at the game's copy if it ever asks.
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var wanted = new AssemblyName(args.Name).Name;
            if (wanted != "Newtonsoft.Json") return null;
            Assembly found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name == wanted) found = asm;
            Debug.Log($"[AP] Resolved {args.Name} -> {found?.FullName ?? "<not found>"}");
            return found;
        }

        [ConsoleCommand("Connect to Archipelago with the server, slot and password from the mod config.", "ap_connect")]
        private void ApConnect() => ApClient.Connect(config.server, config.slot, config.password);

        [ConsoleCommand("Log in and bind the loaded (fresh) profile to the seed and slot you are connecting to.", "ap_bind")]
        private void ApBind() => ApClient.ConfirmBind(force: false);

        [ConsoleCommand("Bind the loaded profile even if it isn't fresh (recovery only).", "ap_bind_force")]
        private void ApBindForce() => ApClient.ConfirmBind(force: true);

        [ConsoleCommand("Disconnect from Archipelago.", "ap_disconnect")]
        private void ApDisconnect() => ApClient.Disconnect("Disconnected.");

        [ConsoleCommand("Show the Archipelago connection and profile status.", "ap_status")]
        private void ApStatus() =>
            ApClient.Say($"{ApClient.Status}; server={config.server} slot={config.slot}; profile " +
                         $"'{DewSave.profileMain?.name}' marker={ProfileGuard.MarkerOf(DewSave.profileMain) ?? "<none>"} " +
                         $"bound={ProfileGuard.Bound}; received {ApClient.ReceivedItems.Count} items");

        [ConsoleCommand("Set the Archipelago server address, e.g. archipelago.gg:38281.", "ap_server")]
        private void ApServer(string address) => SetConfig(() => config.server = address, "server = " + address);

        [ConsoleCommand("Set your Archipelago slot name.", "ap_slot")]
        private void ApSlot(string slot) => SetConfig(() => config.slot = slot, "slot = " + slot);

        [ConsoleCommand("Set the Archipelago room password.", "ap_password")]
        private void ApPassword(string password) => SetConfig(() => config.password = password, "password set");

        private void SetConfig(Action change, string what)
        {
            change();
            try { SaveConfigsToDisk(); }
            catch (Exception e) { Debug.LogException(e); }
            ApClient.Say("Config: " + what);
        }

        // Minimal IMGUI overlay until a proper in-game UI exists. The offline notice is always visible on a bound
        // profile that isn't connected (DESIGN.md "Offline play").
        private void OnGUI()
        {
            if (_offlineStyle == null)
            {
                _offlineStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };
                _offlineStyle.normal.textColor = new Color(1f, 0.45f, 0.35f);
            }
            GUILayout.BeginArea(new Rect(10, 10, 760, 420));
            if (ProfileGuard.Marked && !ProfileGuard.Bound)
                GUILayout.Label("OFFLINE — checks will send on reconnect", _offlineStyle);
            else if (ProfileGuard.Bound)
                GUILayout.Label($"Archipelago: {ApClient.SlotName}");
            else if (ApClient.Status != ApClient.State.Disconnected)
                GUILayout.Label($"Archipelago: {ApClient.Status}");
            float now = Time.unscaledTime;
            foreach (var (text, time) in _feed)
                if (now - time < FeedSeconds)
                    GUILayout.Label(text);
            GUILayout.EndArea();
        }
    }
}
