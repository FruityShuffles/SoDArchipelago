using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Archipelago.MultiClient.Net.Models;
using SoDArchipelago;

internal static class Program
{
    private static int _assertions;
    private static void Check(bool condition, string why)
    {
        _assertions++;
        if (!condition) throw new Exception(why);
    }

    private static void Reset()
    {
        DewSave.profileMain = new DewProfile();
        DewSave.profileMain.experienceFlags.Add("Archipelago:test:slot");
        DewSave.profileStats = new DewProfileStats();
        DewSave.profileMainPath = "test.json";
        ProfileGuard.OnProfileLoaded("test", true);
        ProfileGuard.SetSessionMarker("Archipelago:test:slot");
        DewSave.saves = 0;
        ApClient.IsConnected = true;
        ApClient.ReceivedItems.Clear();
        ApClient.Notices.Clear();
        ApClient.Sent.Clear();
        ApClient.CheckedLocations.Clear();
        ApClient.SlotData.Clear();
        SingletonBehaviour<UI_Constellations>.instance = null;
        Mirror.NetworkServer.active = true;
        NetworkedManagerBase<GameManager>.instance = new GameManager();
        NetworkedManagerBase<ZoneManager>.instance = new ZoneManager();
        NetworkedManagerBase<PingManager>.instance = new PingManager();
        DewResources.modifiers.Clear();
        DewResources.treasures.Clear();
        DewResources.curses.Clear();
        DewResources.fullCurses.Clear();
        Dew.weightedCurses.Clear();
        Dew.curseIncluded = name => true;
        UnityEngine.Random.value = 0f;
        DewResources.treasures["Treasure_CloakOfGuidance"] = DewResources.treasure;
        Dew.spawned.Clear();
        NetworkedManagerBase<QuestManager>.instance = new QuestManager();
        DeathLinkHandler.Reset();
        ApClient.deathLinkEnabled = false;
        ApClient.DeathLinks.Clear();
        DewPlayer.local = new DewPlayer { hero = new Hero() };
        GameData.ItemsById.Clear();
        GameData.LocationsByKey.Clear();
        GameData.Souvenirs.Clear();
        GameData.Artifacts.Clear();
        InRunItems.Cleanup();
    }

    private static void Stardust()
    {
        foreach (int slots in new[] { 17, 80, 137, 152, 279 })
        foreach (int total in new[] { 0, 1, 17, 12345, 54000, 1000000 })
        {
            var values = Enumerable.Range(1, slots).Select(n => StardustAllocation.Value(n, total, slots)).ToArray();
            Check(values.Sum() == total, "Seed total must be exact");
            Check(values.Max() - values.Min() <= 1, "Packs differ by at most one");
            int done = slots / 3;
            int first = StardustAllocation.Cumulative(done, total, slots);
            int rest = StardustAllocation.Cumulative(slots, total, slots) - first;
            Check(first + rest == total, "Reconnect/batches preserve the remainder");
            Check(StardustAllocation.Value(slots + 1, total, slots) == total / slots,
                "Server-granted extras use the quotient");
        }
    }

    private static void Delivery()
    {
        Reset();
        var blessing = new GameData.Item { Id = 1, Key = "BLESSING_TEST", Name = "Test Blessing", Kind = "blessing" };
        var treasure = new GameData.Item { Id = 2, Key = "TREASURE_TEST", Name = "Test Treasure", Kind = "treasure" };
        GameData.ItemsById[1] = blessing;
        GameData.ItemsById[2] = treasure;
        ApClient.ReceivedItems.AddRange(new[] { new ItemInfo { ItemId = 1, LocationId = 10 },
            new ItemInfo { ItemId = 1, LocationId = 11 }, new ItemInfo { ItemId = 2, LocationId = 12 } });
        InRunItems.Update();
        Check(ApRecords.GetApplied(false, blessing.Key) == 0, "Unregistered kinds wait");
        int calls = 0;
        bool target = false;
        InRunItems.Register("blessing", (item, info) => { calls++; return target; });
        InRunItems.Register("treasure", (item, info) => true);
        InRunItems.Update();
        Check(calls == 1, "An unavailable item is tried once per update");
        Check(ApRecords.GetApplied(false, blessing.Key) == 0, "No target must leave counters unchanged");
        Check(ApRecords.GetApplied(false, treasure.Key) == 1, "Later kinds can still land");
        Check(ApClient.Notices.Single().Contains("Test Treasure from Alice"), "Landing notice identifies sender");

        target = true;
        Action<Action, Action, string> blocked = (disable, restore, why) =>
        {
            int before = calls;
            disable(); InRunItems.Update(); restore();
            Check(calls == before, why);
        };
        blocked(() => ApClient.IsConnected = false, () => ApClient.IsConnected = true, "Offline delivery is blocked");
        blocked(() => Mirror.NetworkServer.active = false, () => Mirror.NetworkServer.active = true, "Guests wait");
        var gm = NetworkedManagerBase<GameManager>.instance;
        blocked(() => NetworkedManagerBase<GameManager>.instance = null,
            () => NetworkedManagerBase<GameManager>.instance = gm, "Lobby items wait");
        blocked(() => gm.ready = false, () => gm.ready = true, "Transitions wait");
        blocked(() => gm.isGameConcluded = true, () => gm.isGameConcluded = false, "Ended runs wait");
        blocked(() => NetworkedManagerBase<ZoneManager>.instance.currentZone = null,
            () => NetworkedManagerBase<ZoneManager>.instance.currentZone = new object(), "Missing worlds wait");
        blocked(() => ProfileGuard.SetSessionMarker("Archipelago:other:slot"),
            () => ProfileGuard.SetSessionMarker("Archipelago:test:slot"), "Other profiles must be untouched");
        blocked(() => ProfileGuard.OnProfileLoaded("failed", false),
            () => ProfileGuard.OnProfileLoaded("loaded", true), "Failed loads block delivery");
        InRunItems.Update();
        Check(ApRecords.GetApplied(false, blessing.Key) == 2, "All pending copies land once eligible");
        Check(DewSave.saves == 3, "Each landing saves its applied counter");
        var persisted = new List<string>(DewSave.profileMain.experienceFlags);
        DewSave.profileMain = new DewProfile { experienceFlags = persisted };
        int after = calls;
        InRunItems.Update();
        Check(calls == after, "Restored counters prevent replay after restart/reconnect");
        Check(ApClient.Notices.Count == 3, "Already applied items produce no repeated notices");
    }

    private static void Blessings()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("game_data.json");
        using var data = JsonDocument.Parse(stream);
        var items = data.RootElement.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("kind").GetString() == "blessing")
            .Select(i => new GameData.Item { Id = i.GetProperty("id").GetInt64(),
                Key = i.GetProperty("key").GetString(), Name = i.GetProperty("name").GetString(),
                Target = i.GetProperty("target").GetString(), Kind = "blessing" }).ToArray();
        Check(items.Length == 15, "The delivery tests cover every catalog blessing");
        var main = new HashSet<string> { "RoomMod_PureDream", "RoomMod_GoldEverywhere",
            "RoomMod_HarderFightBetterReward", "RoomMod_GiftMerchant" };
        foreach (var item in items)
        {
            Reset(); MapBlessings.Initialize();
            GameData.ItemsById[item.Id] = item;
            DewResources.modifiers[item.Target] = new RoomModifierBase { isMain = main.Contains(item.Target) };
            ApClient.ReceivedItems.Add(new ItemInfo { ItemId = item.Id, LocationId = 10 });
            var zm = NetworkedManagerBase<ZoneManager>.instance;
            var pm = NetworkedManagerBase<PingManager>.instance;
            zm.canSelect = settings => false;
            InRunItems.Update();
            Check(zm.additions.Count == 0 && pm.pings.Count == 0 && DewSave.saves == 0 && ApClient.Notices.Count == 0 &&
                ApRecords.GetApplied(false, item.Key) == 0, "No vanilla target leaves a blessing pending without feedback");
            var settings = zm.searches.Single();
            Check(settings.allowedTypes.SequenceEqual(new[] { WorldNodeType.Combat }) && settings.preferCloserToExit &&
                settings.desiredDistance.x == (main.Contains(item.Target) ? 2 : 3) && settings.desiredDistance.y == 4 &&
                settings.avoidMainModifier == main.Contains(item.Target), "Use the matching vanilla treasure-map settings");
            zm.canSelect = s => true;
            InRunItems.Update();
            var added = zm.additions.Single();
            var ping = pm.pings.Single();
            Check(added.node == 2 && added.mod.type == item.Target && added.mod.isForceRevealed,
                "Every blessing adds its own force-revealed vanilla modifier at the selected node");
            Check(ping.sender == DewPlayer.local && ping.type == PingManager.PingType.WorldNode && ping.itemIndex == added.node,
                "Broadcast a vanilla world-node ping from the host at the exact destination");
            Check(ApRecords.GetApplied(false, item.Key) == 1 && DewSave.saves == 1 &&
                ApClient.Notices.Single() == $"Blessing from Alice: {item.Name.Substring(10)}, marked on your map.",
                "A successful placement saves once and names the blessing and sender");
            InRunItems.Update();
            Check(zm.additions.Count == 1 && pm.pings.Count == 1 && ApClient.Notices.Count == 1,
                "Already placed blessings are not placed or announced again");
        }

        Reset(); MapBlessings.Initialize();
        var bonus = items.First(i => i.Target == "RoomMod_PureDream");
        var shrine = items.First(i => i.Target == "RoomMod_SpawnMirrorOfRemorse");
        GameData.ItemsById[bonus.Id] = bonus; GameData.ItemsById[shrine.Id] = shrine;
        DewResources.modifiers[bonus.Target] = new RoomModifierBase { isMain = true };
        DewResources.modifiers[shrine.Target] = new RoomModifierBase();
        ApClient.ReceivedItems.AddRange(new[] { new ItemInfo { ItemId = bonus.Id, LocationId = 10 },
            new ItemInfo { ItemId = bonus.Id, LocationId = 11 }, new ItemInfo { ItemId = shrine.Id, LocationId = 12 } });
        var zone = NetworkedManagerBase<ZoneManager>.instance;
        var pings = NetworkedManagerBase<PingManager>.instance;
        zone.canSelect = settings => !settings.avoidMainModifier;
        InRunItems.Update();
        Check(zone.searches.Count == 2 && zone.additions.Single().mod.type == shrine.Target &&
            ApRecords.GetApplied(false, bonus.Key) == 0 && ApRecords.GetApplied(false, shrine.Key) == 1,
            "A pending main bonus cannot block a shrine that can share a node");
        zone = NetworkedManagerBase<ZoneManager>.instance = new ZoneManager();
        zone.canSelect = settings => zone.additions.Count == 0;
        InRunItems.Update();
        Check(zone.additions.Count == 1 && ApRecords.GetApplied(false, bonus.Key) == 1,
            "Each duplicate needs its own successful placement; the rest can wait for the next world");

        // Reconstruct the profile and handler as a restart would, retaining only the persisted counters.
        var flags = new List<string>(DewSave.profileMain.experienceFlags);
        DewSave.profileMain = new DewProfile { experienceFlags = new List<string>(flags) };
        InRunItems.Cleanup(); MapBlessings.Initialize();
        zone = NetworkedManagerBase<ZoneManager>.instance = new ZoneManager();
        Action<Action, Action, string> blocked = (disable, restore, why) =>
        {
            int saved = DewSave.saves, notices = ApClient.Notices.Count, pingCount = pings.pings.Count;
            disable(); InRunItems.Update(); restore();
            Check(zone.additions.Count == 0 && DewSave.saves == saved && ApClient.Notices.Count == notices &&
                pings.pings.Count == pingCount, why);
        };
        blocked(() => ApClient.IsConnected = false, () => ApClient.IsConnected = true, "Offline blessings wait");
        blocked(() => Mirror.NetworkServer.active = false, () => Mirror.NetworkServer.active = true, "Guest blessings wait");
        var gm = NetworkedManagerBase<GameManager>.instance;
        blocked(() => NetworkedManagerBase<GameManager>.instance = null,
            () => NetworkedManagerBase<GameManager>.instance = gm, "Lobby blessings wait");
        blocked(() => gm.ready = false, () => gm.ready = true, "Transition blessings wait");
        blocked(() => gm.isGameConcluded = true, () => gm.isGameConcluded = false, "Concluded runs do not get blessings");
        blocked(() => ProfileGuard.SetSessionMarker("Archipelago:other:slot"),
            () => ProfileGuard.SetSessionMarker("Archipelago:test:slot"), "A different seed cannot place blessings");
        blocked(() => ProfileGuard.OnProfileLoaded("failed", false),
            () => ProfileGuard.OnProfileLoaded("loaded", true), "Failed profile loads cannot place blessings");
        blocked(() => DewSave.profileMainPath = null, () => DewSave.profileMainPath = "test.json",
            "Transient profiles cannot place blessings");
        blocked(() => DewSave.profileMain.experienceFlags.Clear(), () => DewSave.profileMain.experienceFlags.AddRange(flags),
            "Vanilla profiles cannot place blessings");
        blocked(() => NetworkedManagerBase<PingManager>.instance = null,
            () => NetworkedManagerBase<PingManager>.instance = pings, "Wait until vanilla ping feedback is available");
        var nodes = zone.nodes.ToArray();
        blocked(() => zone.nodes.Clear(), () => zone.nodes.AddRange(nodes), "Empty maps cannot be searched");
        blocked(() => DewResources.modifiers.Remove(bonus.Target),
            () => DewResources.modifiers[bonus.Target] = new RoomModifierBase { isMain = true }, "Missing prefabs stay pending");
        InRunItems.Update();
        Check(zone.additions.Count == 1 && ApRecords.GetApplied(false, bonus.Key) == 2 &&
            ApRecords.GetApplied(false, shrine.Key) == 1, "Reconnect lands only the remaining copy in the next world");

        // A failed ping is feedback only: replay would grant another room modifier.
        pings.throwOnPing = true;
        ApClient.ReceivedItems.Add(new ItemInfo { ItemId = bonus.Id, LocationId = -1 });
        InRunItems.Update(); InRunItems.Update();
        Check(zone.additions.Count == 2 && ApRecords.GetApplied(false, bonus.Key) == 3 &&
            ApClient.Notices.Last() == "Blessing from server: Pure Dream, marked on your map.",
            "Ping failure cannot replay a successfully placed server-granted blessing");
        ApClient.ReceivedItems.Add(new ItemInfo { ItemId = bonus.Id, LocationId = -2 });
        InRunItems.Update();
        Check(ApClient.Notices.Last() == "Blessing from starting items: Pure Dream, marked on your map.",
            "Starting blessings have a readable source");
    }

    private static void TreasuresAndDeathLink()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("game_data.json");
        using var data = JsonDocument.Parse(stream);
        var items = data.RootElement.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("kind").GetString() == "treasure")
            .Select(i => new GameData.Item { Id = i.GetProperty("id").GetInt64(),
                Key = i.GetProperty("key").GetString(), Name = i.GetProperty("name").GetString(),
                Target = i.GetProperty("target").GetString(), Kind = "treasure" }).ToArray();
        Check(items.Length == 5, "Test all five actual catalog Treasures");
        foreach (var item in items)
        {
            Reset(); Treasures.Initialize();
            GameData.ItemsById[item.Id] = item;
            var prefab = new Treasure();
            DewResources.treasures[item.Target] = prefab;
            var zone = NetworkedManagerBase<ZoneManager>.instance;
            var player = DewPlayer.local;
            bool map = item.Target.EndsWith("TreasureMap", StringComparison.Ordinal);
            bool clairvoyance = item.Target == "Treasure_Clairvoyance";
            bool cloak = item.Target == "Treasure_CloakOfGuidance";
            ApClient.ReceivedItems.AddRange(new[] { new ItemInfo { ItemId = item.Id, LocationId = 10 },
                new ItemInfo { ItemId = item.Id, LocationId = 11 } });
            Action<Action, Action, string> blocked = (disable, restore, why) =>
            {
                disable(); InRunItems.Update(); restore();
                Check(Dew.spawned.Count == 0 && DewSave.saves == 0 && ApClient.Notices.Count == 0 &&
                    ApRecords.GetApplied(false, item.Key) == 0, why);
            };
            blocked(() => ApClient.IsConnected = false, () => ApClient.IsConnected = true, "Offline Treasures wait");
            blocked(() => Mirror.NetworkServer.active = false, () => Mirror.NetworkServer.active = true, "Guest Treasures wait");
            var gm = NetworkedManagerBase<GameManager>.instance;
            blocked(() => NetworkedManagerBase<GameManager>.instance = null,
                () => NetworkedManagerBase<GameManager>.instance = gm, "Lobby Treasures wait");
            blocked(() => gm.ready = false, () => gm.ready = true, "Transition Treasures wait");
            blocked(() => gm.isGameConcluded = true, () => gm.isGameConcluded = false, "Concluded runs wait");
            blocked(() => ProfileGuard.SetSessionMarker("Archipelago:other:slot"),
                () => ProfileGuard.SetSessionMarker("Archipelago:test:slot"), "Wrong seed cannot deliver Treasures");
            blocked(() => ProfileGuard.OnProfileLoaded("failed", false),
                () => ProfileGuard.OnProfileLoaded("loaded", true), "Failed loads cannot deliver Treasures");
            blocked(() => DewSave.profileMainPath = null, () => DewSave.profileMainPath = "test.json", "Transient profiles wait");
            blocked(() => DewSave.profileMain.experienceFlags.Clear(),
                () => DewSave.profileMain.experienceFlags.Add("Archipelago:test:slot"), "Vanilla profiles remain untouched");
            blocked(() => DewPlayer.local = null, () => DewPlayer.local = player, "Missing player waits");
            var hero = player.hero;
            blocked(() => player.hero = null, () => player.hero = hero, "Missing hero waits");
            blocked(() => hero.isActive = false, () => hero.isActive = true, "Inactive hero waits");
            blocked(() => DewResources.treasures.Remove(item.Target),
                () => DewResources.treasures[item.Target] = prefab, "Missing prefab waits");
            if (cloak)
                blocked(() => zone.isHuntAdvanceDisabled = true, () => zone.isHuntAdvanceDisabled = false,
                    "Cloak waits when Hunters cannot advance, including Primus");
            if (clairvoyance)
                blocked(() => prefab.eligible = false, () => prefab.eligible = true,
                    "Clairvoyance waits when vanilla's unrevealed-area predicate fails");
            if (map)
            {
                var quests = NetworkedManagerBase<QuestManager>.instance;
                blocked(() => NetworkedManagerBase<QuestManager>.instance = null,
                    () => NetworkedManagerBase<QuestManager>.instance = quests, "Maps wait for the quest manager");
                var nodes = zone.nodes.ToArray();
                blocked(() => zone.nodes.Clear(), () => zone.nodes.AddRange(nodes), "Maps wait on empty worlds");
                int searches = zone.searches.Count;
                blocked(() => zone.canSelect = s => false, () => zone.canSelect = s => true,
                    "Maps wait without starting a doomed quest when no node is eligible");
                Check(zone.searches.Count == searches + 1, "Pending map duplicates are tried once per update");
                bool genuine = item.Target == "Treasure_TotallyGenuineTreasureMap";
                var settings = zone.searches.Last();
                Check(settings.desiredDistance.x == (genuine ? 2 : 3) && settings.desiredDistance.y == 4 &&
                    settings.preferCloserToExit && settings.avoidMainModifier == genuine &&
                    settings.allowedTypes.SequenceEqual(new[] { WorldNodeType.Combat }), "Preflight uses the quest's own settings");
            }
            // Simulate the vanilla reveal changing eligibility during this batch; the next copy must wait.
            if (clairvoyance) prefab.onSpawn = t => prefab.eligible = false;
            InRunItems.Update();
            int landed = clairvoyance ? 1 : 2;
            Check(Dew.spawned.Count == landed && ApRecords.GetApplied(false, item.Key) == landed &&
                DewSave.saves == landed && ApClient.Notices.Count == landed, "Each successful Treasure saves and announces its own copy");
            foreach (var spawned in Dew.spawned)
                Check(spawned.player == player && spawned.hero == hero && spawned.price == 0 &&
                    spawned.merchant == null && spawned.customData == null && spawned.position.x == hero.agentPosition.x &&
                    spawned.position.y == hero.agentPosition.y && spawned.position.z == hero.agentPosition.z &&
                    spawned.destroyed == clairvoyance, "Assign free Treasure fields before OnCreate at the local hero; clean up Clairvoyance");
            Check(prefab.player == null && prefab.hero == null, "Do not mutate the shared Treasure prefab");
            Check(ApClient.Notices.All(n => n.Contains(item.Name + " from Alice")), "Landing notices name each Treasure and sender");
            var flags = new List<string>(DewSave.profileMain.experienceFlags);
            DewSave.profileMain = new DewProfile { experienceFlags = flags };
            InRunItems.Cleanup(); Treasures.Initialize();
            InRunItems.Update();
            Check(Dew.spawned.Count == landed, "Persisted Treasure counters prevent replay after restart");
            if (clairvoyance)
            {
                prefab.eligible = true; prefab.throwOnDestroy = true;
                InRunItems.Update(); InRunItems.Update();
                Check(Dew.spawned.Count == 2 && ApRecords.GetApplied(false, item.Key) == 2,
                    "Next world's reveal lands the pending copy once, even if cleanup fails");
            }
        }

        Reset(); ApClient.deathLinkEnabled = true;
        var local = DewPlayer.local.hero;
        local.onKill = () => { }; // A Shard absorbs the kill: neither knockdown nor bleed-out.
        DeathLinkHandler.OnDeathLinkReceived("Alice", "fell");
        Check(local.kills == 1 && ApClient.DeathLinks.Count == 0, "A received kill absorbed by a Shard sends no DeathLink");
        DeathLinkHandler.OnHeroKnockedOut(local);
        Check(ApClient.DeathLinks.Count == 1, "The next real knockdown still sends after an absorbed DeathLink");
        ApClient.DeathLinks.Clear();
        local.onKill = () => { local.isKnockedOut = true; }; // RPC may be delivered after Kill returns.
        DeathLinkHandler.OnDeathLinkReceived("Alice", "fell");
        DeathLinkHandler.OnHeroKnockedOut(local);
        Check(ApClient.DeathLinks.Count == 0, "Received DeathLink knockdown is suppressed even with a later RPC");
        DeathLinkHandler.OnHeroRevive(local); local.isKnockedOut = false;
        local.onKill = () => { local.Status.effects.Add(typeof(Se_HeroBleedingOut)); };
        DeathLinkHandler.OnDeathLinkReceived("Alice", "fell");
        local.Status.effects.Clear(); local.isKnockedOut = true;
        DeathLinkHandler.OnHeroKnockedOut(local);
        Check(ApClient.DeathLinks.Count == 0, "Bleed-out's later knockdown retains DeathLink suppression");
        local.isKnockedOut = false;
        local.onKill = () => { local.isKnockedOut = true; DeathLinkHandler.OnHeroKnockedOut(local); };
        DeathLinkHandler.OnDeathLinkReceived("Alice", "fell");
        Check(ApClient.DeathLinks.Count == 0, "A synchronous host knockdown also suppresses the received DeathLink");
        local.isKnockedOut = false;
        DeathLinkHandler.OnHeroKnockedOut(new Hero());
        Check(ApClient.DeathLinks.Count == 0, "Another player's knockdown is ignored");
        DeathLinkHandler.OnHeroKnockedOut(local);
        Check(ApClient.DeathLinks.Count == 1, "Consuming suppression does not suppress a subsequent normal death");
        int kills = local.kills;
        Mirror.NetworkServer.active = false;
        DeathLinkHandler.OnDeathLinkReceived("Alice", "fell");
        Check(local.kills == kills, "Guests cannot apply received DeathLinks");
        Mirror.NetworkServer.active = true; ProfileGuard.SetSessionMarker("Archipelago:other:slot");
        DeathLinkHandler.OnDeathLinkReceived("Alice", "fell"); DeathLinkHandler.OnHeroKnockedOut(local);
        Check(local.kills == kills && ApClient.DeathLinks.Count == 1, "A different profile cannot receive or send DeathLinks");
    }

    private static void Records()
    {
        Reset();
        foreach (var kind in new[] { "ware", "shrine", "quest" })
            GameData.LocationsByKey[kind] = new GameData.Location { Id = GameData.LocationsByKey.Count + 1,
                Name = kind, Key = kind, Kind = kind };
        ProfileGuard.SetSessionMarker(null);
        foreach (var key in GameData.LocationsByKey.Keys)
        {
            Check(CheckHandler.RecordCheck(key), "First offline check records");
            Check(!CheckHandler.RecordCheck(key), "Repeated checks do not record again");
        }
        Check(DewSave.saves == 3 && ApClient.Sent.Count == 0, "Offline checks save without sending");
        Check(ApRecords.Checks().Count() == 3, "All new kinds share the profile record");
        Check(!CheckHandler.RecordCheck("unknown"), "Unknown keys cannot create records");
        var flags = new List<string>(DewSave.profileMain.experienceFlags);
        DewSave.profileMain = new DewProfile { experienceFlags = flags };
        ProfileGuard.SetSessionMarker("Archipelago:test:slot");
        CheckHandler.ResendAll();
        Check(ApClient.Sent.OrderBy(id => id).SequenceEqual(new long[] { 1, 2, 3 }), "Reconnect resends saved checks");
        DewSave.profileMain = new DewProfile();
        Check(!ApRecords.AddCheck("ware") && !ApRecords.HasCheck("ware") && !ApRecords.Checks().Any(),
            "Unmarked profiles cannot read or write records");
        Check(!CheckHandler.RecordCheck("ware") && DewSave.profileMain.experienceFlags.Count == 0,
            "Vanilla profiles stay untouched");
    }

    private static void StardustItems()
    {
        foreach (int total in new[] { 0, 1, 54000 })
        {
            Reset();
            ApClient.SlotData["stardust_total"] = total;
            ApClient.SlotData["stardust_item_count"] = 137;
            GameData.ItemsById[1] = new GameData.Item { Id = 1, Key = GameData.StardustKey,
                Name = "Stardust", Kind = "stardust" };
            SingletonBehaviour<UI_Constellations>.instance = new UI_Constellations();
            for (int i = 0; i < 50; i++) ApClient.ReceivedItems.Add(new ItemInfo { ItemId = 1 });
            ItemHandler.ProcessAll("first batch");
            int first = DewSave.profileMain.stardust;
            Check(first == StardustAllocation.Cumulative(50, total, 137), "First batch grants its share");
            ItemHandler.ProcessAll("reconnect");
            Check(DewSave.profileMain.stardust == first && ApClient.Notices.Count == 50,
                "Full-list replay does not grant or notify twice");
            for (int i = 50; i < 137; i++) ApClient.ReceivedItems.Add(new ItemInfo { ItemId = 1 });
            ItemHandler.ProcessAll("rest");
            Check(DewSave.profileMain.stardust == total, "All seed Stardust totals exactly");
            Check(SingletonBehaviour<UI_Constellations>.instance.state.stardust == total,
                "Open constellations keep their working currency in sync");
            Check(ApRecords.GetApplied(false, GameData.StardustKey) == 137 && DewSave.saves == 2,
                "Counter and currency save together, including zero-value packs");
            ProfileGuard.SetSessionMarker(null);
            ApClient.ReceivedItems.Add(new ItemInfo { ItemId = 1 });
            ItemHandler.ProcessAll("offline");
            Check(ApRecords.GetApplied(false, GameData.StardustKey) == 137, "Offline items cannot apply");
        }
    }

    private static void Main()
    {
        Stardust(); StardustItems(); Delivery(); Blessings(); TreasuresAndDeathLink(); Curses(); Records(); Wares(); Pilgrimage();
        Console.WriteLine($"Passed {_assertions} assertions (Stardust, delivery, blessings, treasures, curses, DeathLink, checks, wares and pilgrimage).");
    }

    private sealed class ExcludedCurse : CurseStatusEffect { }

    private static void Curses()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("game_data.json");
        using var data = JsonDocument.Parse(stream);
        var items = data.RootElement.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("kind").GetString() == "curse")
            .Select(i => new GameData.Item { Id = i.GetProperty("id").GetInt64(),
                Key = i.GetProperty("key").GetString(), Name = i.GetProperty("name").GetString(),
                Target = i.GetProperty("target").GetString(), Kind = "curse" }).ToArray();
        Check(items.Length == 3, "Test every catalog curse tier");
        foreach (var item in items)
        {
            Reset(); CurseTraps.Initialize();
            GameData.ItemsById[item.Id] = item;
            ApClient.ReceivedItems.AddRange(new[] { new ItemInfo { ItemId = item.Id, LocationId = 10 },
                new ItemInfo { ItemId = item.Id, LocationId = 11 } });
            var prefab = new CurseStatusEffect();
            DewResources.curses.Add(prefab);
            var zone = NetworkedManagerBase<ZoneManager>.instance;
            var player = DewPlayer.local;
            var hero = player.hero;
            var gm = NetworkedManagerBase<GameManager>.instance;
            Action<Action, Action, string> blocked = (disable, restore, why) =>
            {
                disable(); InRunItems.Update(); restore();
                Check(hero.curses.Count == 0 && DewSave.saves == 0 && ApClient.Notices.Count == 0 &&
                    ApRecords.GetApplied(false, item.Key) == 0, why);
            };
            blocked(() => ApClient.IsConnected = false, () => ApClient.IsConnected = true, "Offline curses wait");
            blocked(() => Mirror.NetworkServer.active = false, () => Mirror.NetworkServer.active = true, "Joining players wait");
            blocked(() => NetworkedManagerBase<GameManager>.instance = null,
                () => NetworkedManagerBase<GameManager>.instance = gm, "Lobby curses wait");
            blocked(() => gm.ready = false, () => gm.ready = true, "Run startup curses wait");
            blocked(() => gm.isGameConcluded = true, () => gm.isGameConcluded = false, "Ended runs wait");
            blocked(() => ProfileGuard.SetSessionMarker("Archipelago:other:slot"),
                () => ProfileGuard.SetSessionMarker("Archipelago:test:slot"), "Wrong seed cannot curse");
            blocked(() => ProfileGuard.OnProfileLoaded("failed", false),
                () => ProfileGuard.OnProfileLoaded("loaded", true), "Failed profile loads cannot curse");
            blocked(() => DewSave.profileMainPath = null, () => DewSave.profileMainPath = "test.json", "Transient profiles wait");
            blocked(() => DewSave.profileMain.experienceFlags.Clear(),
                () => DewSave.profileMain.experienceFlags.Add("Archipelago:test:slot"), "Vanilla profiles remain untouched");
            blocked(() => DewPlayer.local = null, () => DewPlayer.local = player, "Missing local player waits");
            blocked(() => player.hero = null, () => player.hero = hero, "Missing hero waits");
            blocked(() => hero.isActive = false, () => hero.isActive = true, "Inactive hero waits");
            blocked(() => hero.isKnockedOut = true, () => hero.isKnockedOut = false, "Knocked out hero waits");
            blocked(() => hero.Status.isDead = true, () => hero.Status.isDead = false, "Dead hero waits");
            blocked(() => hero.Status.effects.Add(typeof(Se_HeroBleedingOut)),
                () => hero.Status.effects.Clear(), "Bleeding out hero waits");
            blocked(() => zone.isInRoomTransition = true, () => zone.isInRoomTransition = false, "Room transitions wait");
            var room = zone.currentRoom;
            blocked(() => zone.currentRoom = null, () => zone.currentRoom = room, "Missing room waits");
            blocked(() => zone.currentNodeIndex = -1, () => zone.currentNodeIndex = 0, "Uninitialized node waits");
            blocked(() => zone.currentNodeIndex = zone.nodes.Count, () => zone.currentNodeIndex = 0, "Invalid node waits");
            blocked(() => zone.currentNode.type = WorldNodeType.ExitBoss,
                () => zone.currentNode.type = WorldNodeType.Combat, "World and Primus boss rooms wait, even after their boss dies");
            zone.currentNode.type = WorldNodeType.Special;
            blocked(() => room.name = "Room_Special_StarlessPath_BossPolaris",
                () => room.name = "Room_Combat", "Polaris's special sidetrack boss room also waits");
            zone.currentNode.type = WorldNodeType.Combat;
            var quests = NetworkedManagerBase<QuestManager>.instance;
            blocked(() => NetworkedManagerBase<QuestManager>.instance = null,
                () => NetworkedManagerBase<QuestManager>.instance = quests, "Missing quest tracker waits");
            blocked(() => DewResources.curses.Clear(), () => DewResources.curses.Add(prefab), "Empty curse pool waits");
            blocked(() => prefab.availableStrengths = HatredStrengthType.None,
                () => prefab.availableStrengths = HatredStrengthType.Mild | HatredStrengthType.Potent | HatredStrengthType.Powerful,
                "Unsupported tier leaves each received copy pending");
            blocked(() => prefab.viable = h => false, () => prefab.viable = h => true, "Nonviable curses wait");
            blocked(() => prefab.chanceWeight = 0, () => prefab.chanceWeight = 1, "All zero weights cannot select entry zero");
            blocked(() => Dew.curseIncluded = name => false, () => Dew.curseIncluded = name => true, "Excluded game content waits");
            blocked(() => hero.rejectCurse = true, () => hero.rejectCurse = false, "A null creation result leaves copies pending");
            blocked(() => item.Target = "Powerful", () => item.Target = item.Name.Substring(7), "Unknown catalog targets wait");

            var excluded = new ExcludedCurse { chanceWeight = 1000 };
            var nonviable = new CurseStatusEffect { chanceWeight = 1000, viable = h => false };
            var wrongTier = new CurseStatusEffect { chanceWeight = 1000, availableStrengths = HatredStrengthType.None };
            var zero = new CurseStatusEffect { chanceWeight = 0 };
            var negative = new CurseStatusEffect { chanceWeight = -1 };
            var second = new CurseStatusEffect { chanceWeight = 7 };
            var light = new CurseStatusEffect { chanceWeight = 1000, availableStrengths = HatredStrengthType.None };
            DewResources.fullCurses[light] = second;
            DewResources.curses.AddRange(new CurseStatusEffect[] { excluded, nonviable, wrongTier, zero, negative, light });
            Dew.curseIncluded = name => name != nameof(ExcludedCurse);
            nonviable.viable = h => { Check(h == hero, "Viability is tested against the local host"); return false; };
            zone.currentZoneIndex = 4;
            InRunItems.Update();
            var strength = item.Target == "Mild" ? HatredStrengthType.Mild :
                item.Target == "Potent" ? HatredStrengthType.Potent : HatredStrengthType.Powerful;
            int kills = item.Target == "Mild" ? 36 : item.Target == "Potent" ? 46 : 62;
            Check(hero.curses.Count == 2 && hero.curses.All(c => c.currentStrength == strength &&
                c.skillLevel == (int)(strength - 1) && c.progressType == QuestProgressType.Kills && c.requiredAmount == kills),
                "Every duplicate receives the exact shrine tier, skill level and world-scaled kill condition");
            Check(Dew.weightedCurses.Select(c => c.curse).SequenceEqual(new[] { prefab, second }) &&
                Dew.weightedCurses.Select(c => c.weight).SequenceEqual(new[] { 1f, 7f }), "Only eligible positive weights reach vanilla selection");
            Check(hero.curses.All(c => c.source == second && c.victim == hero && c.parent == hero &&
                c.info.caster == hero && c.info.target == hero), "The chosen vanilla effect targets only the host, with the shrine's CastInfo");
            Check(ApRecords.GetApplied(false, item.Key) == 2 && DewSave.saves == 2 &&
                ApClient.Notices.All(n => n == $"Received {item.Name} from Alice"), "Each successful curse saves and identifies its sender");
            Check(prefab.currentStrength == HatredStrengthType.None && second.requiredAmount == 0,
                "Shared curse prefabs are never mutated");
            var flags = new List<string>(DewSave.profileMain.experienceFlags);
            DewSave.profileMain = new DewProfile { experienceFlags = flags };
            InRunItems.Cleanup(); CurseTraps.Initialize();
            InRunItems.Update();
            Check(hero.curses.Count == 2, "Restarted counters prevent replay of spent curses");

            foreach (int world in new[] { 0, 8 })
            {
                zone.currentZoneIndex = world;
                ApClient.ReceivedItems.Add(new ItemInfo { ItemId = item.Id, LocationId = -1 });
                UnityEngine.Random.value = 0.499f;
                InRunItems.Update();
                Check(hero.curses.Last().requiredAmount == (item.Target == "Mild" ? 28 + world * 2 :
                    (item.Target == "Potent" ? 34 : 50) + world * 3), "Kill counts follow the shrine through later loops");
                ApClient.ReceivedItems.Add(new ItemInfo { ItemId = item.Id, LocationId = -2 });
                UnityEngine.Random.value = 0.5f;
                InRunItems.Update();
                Check(hero.curses.Last().progressType == QuestProgressType.Travel &&
                    hero.curses.Last().requiredAmount == (item.Target == "Mild" ? 3 : 4), "Travel is the other half of vanilla's lift-condition split");
            }
            Check(ApClient.Notices.Contains($"Received {item.Name} (server)") &&
                ApClient.Notices.Contains($"Received {item.Name} (starting item)"), "Server and starting curse grants identify their source");
        }

        Reset(); CurseTraps.Initialize(); MapBlessings.Initialize();
        var curse = items[0];
        var blessing = new GameData.Item { Id = 1, Key = "BLESSING_TEST", Kind = "blessing", Target = "RoomMod_PureDream", Name = "Blessing: Pure Dream" };
        GameData.ItemsById[curse.Id] = curse; GameData.ItemsById[1] = blessing;
        DewResources.curses.Add(new CurseStatusEffect());
        DewResources.modifiers[blessing.Target] = new RoomModifierBase();
        ApClient.ReceivedItems.AddRange(new[] { new ItemInfo { ItemId = curse.Id }, new ItemInfo { ItemId = 1 } });
        var next = NetworkedManagerBase<ZoneManager>.instance;
        next.currentNode.type = WorldNodeType.ExitBoss;
        InRunItems.Update();
        Check(ApRecords.GetApplied(false, curse.Key) == 0 && ApRecords.GetApplied(false, blessing.Key) == 1,
            "A curse waiting in a boss room cannot block a later blessing");
        next.currentNode.type = WorldNodeType.Merchant;
        InRunItems.Update();
        Check(ApRecords.GetApplied(false, curse.Key) == 1 && DewPlayer.local.hero.curses.Count == 1,
            "The pending curse lands after leaving the boss room, including in a shop");
        InRunItems.Cleanup();
        ApClient.ReceivedItems.Add(new ItemInfo { ItemId = curse.Id });
        InRunItems.Update();
        Check(DewPlayer.local.hero.curses.Count == 1, "Mod cleanup unregisters curse delivery");

        Reset(); CurseTraps.Initialize();
        GameData.ItemsById[curse.Id] = curse;
        ApClient.ReceivedItems.AddRange(new[] { new ItemInfo { ItemId = curse.Id }, new ItemInfo { ItemId = curse.Id } });
        var limited = new CurseStatusEffect { viable = h => ((Hero)h).curses.Count == 0 };
        DewResources.curses.Add(limited);
        InRunItems.Update(); InRunItems.Update();
        Check(DewPlayer.local.hero.curses.Count == 1 && ApRecords.GetApplied(false, curse.Key) == 1,
            "Viability is rechecked after each creation; a duplicate waits while its curse is already active");
        DewPlayer.local.hero = new Hero(); // A new run has no active curse, but retains AP counters.
        InRunItems.Update();
        Check(DewPlayer.local.hero.curses.Count == 1 && ApRecords.GetApplied(false, curse.Key) == 2,
            "A pending duplicate can land in the next run without replaying the first copy");
        ApClient.ReceivedItems.Add(new ItemInfo { ItemId = curse.Id });
        DewResources.fullCurses[limited] = null;
        DewPlayer.local.hero = new Hero();
        InRunItems.Update();
        Check(DewPlayer.local.hero.curses.Count == 0 && ApRecords.GetApplied(false, curse.Key) == 2,
            "A missing full gameplay asset stays pending instead of spawning the light discovery prefab");
    }

    private static void Wares()
    {
        Reset();
        for (int n = 1; n <= 100; n++)
            GameData.LocationsByKey["WARE_JONAS_" + n] = new GameData.Location { Id = n,
                Key = "WARE_JONAS_" + n, Name = "Jonas's Ware " + n, Kind = "ware", Number = n };
        ApClient.SlotData["jonas_wares"] = 30;
        JonasWares.OnLoggedIn();
        Check(ApRecords.WareCount() == 30 && JonasWares.Enabled().Count == 30, "Seed count persists for offline shops");
        var player = DewPlayer.local = new DewPlayer { guid = "host", hero = new Hero() };
        var guest = new DewPlayer { guid = "guest", hero = new Hero() };
        var shop = new PropEnt_Merchant_Jonas();
        var normal = new MerchandiseData { type = MerchandiseType.Skill, itemName = "Skill", count = 2 };
        shop.merchandises[player.guid] = new[] { normal };
        shop.merchandises[guest.guid] = new[] { normal };
        var guestStock = shop.merchandises[guest.guid];
        JonasWares.Append(shop, guest);
        Check(ReferenceEquals(guestStock, shop.merchandises[guest.guid]), "Guests' stock is untouched");
        var other = new PropEnt_Merchant_Base(); other.merchandises[player.guid] = new[] { normal };
        JonasWares.Append(other, player);
        Check(other.merchandises[player.guid].Length == 1, "Other merchants never sell wares");
        Mirror.NetworkServer.active = false;
        JonasWares.Append(shop, player);
        Check(shop.merchandises[player.guid].Length == 1, "Joining players never add wares");
        Mirror.NetworkServer.active = true;
        JonasWares.Append(shop, player);
        var stock = shop.merchandises[player.guid];
        var ware = stock[1];
        Check(stock.Length == 2 && stock[0].count == 2, "One ware appends without changing vanilla items");
        Check(JonasWares.TryGetWare(ware, out var location) && location.Number == 30, "Random choice stays within enabled wares");
        Check(ware.price.gold == 237 && ware.count == 1 && DewResources.treasure.priceCalls == 1,
            "Ware takes its placeholder's vanilla price, with one purchase");
        Check(JonasWares.Description(location) == "an unknown ware", "Unscouted stock shows unknown contents");
        var contents = new Dictionary<string, (string item, string owner)> {
            [location.Key] = ("Progressive Mist: = <test>\n\u00e9", "Alice: = \u661f"),
            ["WARE_JONAS_100"] = ("disabled", "Bob") };
        JonasWares.CacheScouts(contents);
        Check(ApRecords.TryScout(location.Key, out var item, out var owner) &&
            item == contents[location.Key].item && owner == contents[location.Key].owner, "Scout strings round-trip intact");
        Check(!ApRecords.TryScout("WARE_JONAS_100", out _, out _), "Disabled scouts are not cached");
        Check(JonasWares.Description(location) == item + ", for " + owner, "Shop names the item and its recipient");
        int saved = DewSave.saves;
        JonasWares.CacheScouts(contents);
        Check(DewSave.saves == saved, "Identical scouts cause no redundant save");
        JonasWares.CacheScouts(new Dictionary<string, (string item, string owner)> {
            ["WARE_JONAS_1"] = (null, "Alice"), ["WARE_JONAS_2"] = ("Item", null),
            ["WARE_JONAS_3"] = ("Resolved item", "Bob") });
        Check(!ApRecords.TryScout("WARE_JONAS_1", out _, out _) &&
            !ApRecords.TryScout("WARE_JONAS_2", out _, out _) &&
            ApRecords.TryScout("WARE_JONAS_3", out var resolved, out _) && resolved == "Resolved item" &&
            DewSave.saves == saved + 1, "Unresolved scouts do not interrupt caching or saving later entries");
        Check(JonasWares.CanPurchase(shop, player, 1), "An unbought enabled ware may be charged");
        Check(!JonasWares.Consume(shop, guest, ware) && !JonasWares.Consume(other, player, ware),
            "Guests and other merchants cannot record the host's checks");
        ProfileGuard.SetSessionMarker(null); ApClient.IsConnected = false;
        Check(JonasWares.Description(location) == "an unknown ware", "Offline contents stay unknown");
        Check(JonasWares.Consume(shop, player, ware) && ApRecords.HasCheck(location.Key) && ApClient.Sent.Count == 0,
            "Offline purchase suppresses the placeholder and persists its check");
        Check(!JonasWares.CanPurchase(shop, player, 1) && shop.merchandises[player.guid][1].count == 0,
            "A stale continue-save ware is refused before gold is charged");
        saved = DewSave.saves;
        Check(JonasWares.Consume(shop, player, ware) && DewSave.saves == saved, "Duplicate spawn cannot record twice");
        var persisted = new List<string>(DewSave.profileMain.experienceFlags);
        DewSave.profileMain = new DewProfile { experienceFlags = persisted };
        Check(ApRecords.WareCount() == 30 && ApRecords.TryScout(location.Key, out _, out _) && ApRecords.HasCheck(location.Key),
            "Count, contents and purchases survive profile reload");
        ProfileGuard.SetSessionMarker("Archipelago:test:slot"); ApClient.IsConnected = true;
        CheckHandler.ResendAll();
        Check(ApClient.Sent.SequenceEqual(new[] { location.Id }), "Reconnect resends the offline purchase");
        shop.merchandises[player.guid] = new[] { normal }; // vanilla refresh replaces stock
        JonasWares.Append(shop, player);
        Check(JonasWares.TryGetWare(shop.merchandises[player.guid][1], out var next) && next.Number == 29,
            "Refresh selects from the remaining unbought locations");
        JonasWares.Append(shop, player);
        Check(shop.merchandises[player.guid].Length == 2, "Repeated population never duplicates the extra ware");
        ApClient.CheckedLocations.Add(next.Id);
        Check(!ApRecords.HasCheck(next.Key) && !JonasWares.CanPurchase(shop, player, 1) &&
            shop.merchandises[player.guid][1].count == 0, "A released ware is refused before charging gold");
        JonasWares.Append(shop, player);
        Check(JonasWares.TryGetWare(shop.merchandises[player.guid][1], out var unreleased) &&
            unreleased.Number == 28, "Refresh skips server-checked wares");
        ProfileGuard.SetSessionMarker("Archipelago:other:slot");
        Check(JonasWares.IsAvailable(next), "Another slot's checked list cannot affect the marked profile");
        ProfileGuard.SetSessionMarker("Archipelago:test:slot");
        ApClient.IsConnected = false;
        Check(JonasWares.IsAvailable(next), "Offline stock still uses its local purchase record");
        ApClient.IsConnected = true;
        // A seed with fewer wares must also refuse a saved entry outside its enabled range.
        ApRecords.SetWareCount(1);
        Check(!JonasWares.CanPurchase(shop, player, 1), "A disabled saved ware is refused before charging");
        ApRecords.SetWareCount(30);
        ProfileGuard.SetSessionMarker("Archipelago:other:slot");
        Check(!ApRecords.SetScout(location.Key, "wrong seed", "wrong owner") && !ApRecords.SetWareCount(100),
            "A different session cannot alter the profile's ware settings or contents");
        ProfileGuard.SetSessionMarker("Archipelago:test:slot");
        ProfileGuard.OnProfileLoaded("failed", false);
        Check(!JonasWares.IsLocalHost(player) && JonasWares.Enabled().Count == 0 &&
            !ApRecords.TryScout(location.Key, out _, out _), "Failed profile loads cannot access ware records");
        ProfileGuard.OnProfileLoaded("loaded", true);
        DewSave.profileMainPath = null;
        Check(!JonasWares.IsLocalHost(player), "Transient profiles never modify stock");
        DewSave.profileMainPath = "test.json";
        foreach (var enabled in JonasWares.Enabled()) ApRecords.AddCheck(enabled.Key);
        shop.merchandises[player.guid] = new[] { normal };
        JonasWares.Append(shop, player);
        Check(shop.merchandises[player.guid].Length == 1, "Exhausted seeds have no extra ware");
        ApRecords.SetWareCount(0);
        Check(JonasWares.Enabled().Count == 0, "The zero option disables all wares");
        DewSave.profileMain = new DewProfile();
        JonasWares.Append(shop, player);
        JonasWares.CacheScouts(contents);
        Check(!ApRecords.SetWareCount(30) && !ApRecords.SetScout(location.Key, "item", "owner") &&
            !ApRecords.TryScout(location.Key, out _, out _) && ApRecords.WareCount() == 0 &&
            DewSave.profileMain.experienceFlags.Count == 0, "Vanilla profiles cannot read or write ware data");
    }

    private static void Pilgrimage()
    {
        Reset();
        var player = DewPlayer.local = new DewPlayer();
        var guest = new DewPlayer();
        var user = new Entity { owner = player };
        var shrine = new Shrine_PotOfGreed();
        var quest = new Quest_StrayMemory();
        var shrineLoc = new GameData.Location { Id = 101, Key = "Shrine_PotOfGreed", Name = "Pot of Greed", Kind = "shrine" };
        var questLoc = new GameData.Location { Id = 102, Key = "Quest_StrayMemory", Name = "Stray Memory", Kind = "quest" };
        GameData.LocationsByKey[shrineLoc.Key] = shrineLoc;
        GameData.LocationsByKey[questLoc.Key] = questLoc;
        CheckHandler.OnShrineUsed(shrine, new Entity { owner = guest });
        CheckHandler.OnShrineUsed(shrine, new Entity());
        CheckHandler.OnShrineUsed(shrine, null);
        CheckHandler.OnShrineUsed(null, user);
        CheckHandler.OnShrineUsed(new Shrine_Disintegration(), user);
        Check(ApRecords.Checks().Count() == 0, "Other users, missing users and unlisted shrines do not check");
        // Joining clients must record offline too; no server, difficulty or ForcedDreams condition is required.
        Mirror.NetworkServer.active = false;
        NetworkedManagerBase<GameSettingsManager>.instance = new GameSettingsManager { difficulty = "diffLimbo" };
        ProfileGuard.SetSessionMarker(null); ApClient.IsConnected = false;
        CheckHandler.OnShrineUsed(shrine, user);
        CheckHandler.OnShrineUsed(shrine, user);
        Check(ApRecords.HasCheck(shrineLoc.Key) && DewSave.saves == 1 && ApClient.Sent.Count == 0,
            "First local shrine use records once for an offline joining player");
        CheckHandler.OnQuestRemoved(quest); // removal may arrive before the Completed SyncVar
        quest.state = QuestState.Failed; CheckHandler.OnQuestRemoved(quest);
        CheckHandler.OnQuestRemoved(new Quest_GuidingCompass { state = QuestState.Completed });
        Check(!ApRecords.HasCheck(questLoc.Key), "Ongoing, failed and unlisted quests never check");
        quest.state = QuestState.Completed;
        CheckHandler.OnQuestRemoved(quest); // the post-deserialization hook reads the final state
        CheckHandler.OnQuestRemoved(quest);
        Check(ApRecords.HasCheck(questLoc.Key) && DewSave.saves == 2,
            "Late Completed state records once, including for joining clients");

        var keys = new[] { "Artifact_BouquetOfEyes", "Artifact_EmblemOfSubjugation", "Artifact_FirstMerchantsToken",
            "Artifact_FoolsGold", "Artifact_ForestHoundSeed", "Artifact_NightmareCatalyst", "Artifact_StarBlossom",
            "Artifact_TheStarlitStone", "Artifact_TomeOfTheSeeker", "Artifact_VoidWhisperer", "Artifact_WatchersNote",
            "Artifact_WeddingRing" };
        foreach (var key in keys)
        {
            var location = new GameData.Location { Id = 200 + GameData.Artifacts.Count, Key = key, Name = key, Kind = "artifact" };
            GameData.LocationsByKey[key] = location; GameData.Artifacts.Add(location);
            DewSave.profileMain.artifacts[key] = new DewProfile.Artifact { status = UnlockStatus.NotDiscovered };
            CheckHandler.OnArtifactDiscovered(DewSave.profileMain, key, false);
            Check(!CheckHandler.IsArtifactDiscovered(DewSave.profileMain, key) && DewSave.saves == 2 + GameData.Artifacts.Count - 1,
                "Picking up an artifact without handing it in is not a check");
            bool before = CheckHandler.IsArtifactDiscovered(DewSave.profileMain, key);
            DewSave.profileMain.artifacts[key].status = UnlockStatus.Complete;
            CheckHandler.OnArtifactDiscovered(DewSave.profileMain, key, before);
            int saved = DewSave.saves;
            CheckHandler.OnArtifactDiscovered(DewSave.profileMain, key,
                CheckHandler.IsArtifactDiscovered(DewSave.profileMain, key));
            Check(CheckHandler.IsArtifactDiscovered(DewSave.profileMain, key) && saved == 2 + GameData.Artifacts.Count &&
                DewSave.saves == saved && !ApRecords.HasCheck(key), "Hand-in saves its native flag once, with no AP record");
        }
        var differentProfile = new DewProfile();
        differentProfile.artifacts[keys[0]] = new DewProfile.Artifact { status = UnlockStatus.Complete };
        int writes = DewSave.saves;
        CheckHandler.OnArtifactDiscovered(differentProfile, keys[0], false);
        Check(!CheckHandler.IsArtifactDiscovered(differentProfile, keys[0]) && DewSave.saves == writes,
            "An artifact on a different profile cannot be read or recorded");
        const string excluded = "Artifact_AnOldGratitude";
        DewSave.profileMain.artifacts[excluded] = new DewProfile.Artifact { status = UnlockStatus.Complete };
        CheckHandler.OnArtifactDiscovered(DewSave.profileMain, excluded, false);
        Check(DewSave.saves == writes && ApClient.Sent.Count == 0, "Non-pool artifacts send no checks; offline hand-ins send nothing");
        var flags = new List<string>(DewSave.profileMain.experienceFlags);
        var artifacts = DewSave.profileMain.artifacts.ToDictionary(kv => kv.Key,
            kv => new DewProfile.Artifact { status = kv.Value.status });
        DewSave.profileMain = new DewProfile { experienceFlags = flags, artifacts = artifacts };
        ProfileGuard.SetSessionMarker("Archipelago:test:slot"); ApClient.IsConnected = true;
        CheckHandler.ResendAll();
        Check(ApClient.Sent.OrderBy(id => id).SequenceEqual(new long[] { 101, 102 }.Concat(Enumerable.Range(200, 12).Select(n => (long)n))),
            "Reconnect resends persisted shrine, quest and all twelve native artifact flags");

        // Vanilla, failed and transient profiles must not get AP records or native flag reads.
        ApClient.Sent.Clear();
        DewSave.profileMain.experienceFlags.Clear();
        CheckHandler.OnShrineUsed(shrine, user); CheckHandler.OnQuestRemoved(quest);
        CheckHandler.OnArtifactDiscovered(DewSave.profileMain, keys[0], false); CheckHandler.ResendAll();
        Check(!CheckHandler.IsArtifactDiscovered(DewSave.profileMain, keys[0]) && DewSave.saves == writes &&
            ApClient.Sent.Count == 0 && DewSave.profileMain.experienceFlags.Count == 0, "Vanilla profiles remain untouched");
        DewSave.profileMain.experienceFlags.Add("Archipelago:test:slot");
        ProfileGuard.OnProfileLoaded("failed", false);
        CheckHandler.OnShrineUsed(shrine, user); CheckHandler.OnQuestRemoved(quest);
        CheckHandler.OnArtifactDiscovered(DewSave.profileMain, keys[0], false); CheckHandler.ResendAll();
        Check(ApRecords.Checks().Count() == 0 && !CheckHandler.IsArtifactDiscovered(DewSave.profileMain, keys[0]) &&
            DewSave.saves == writes && ApClient.Sent.Count == 0, "Failed loads block all pilgrimage access");
        ProfileGuard.OnProfileLoaded("loaded", true); DewSave.profileMainPath = null;
        CheckHandler.OnShrineUsed(shrine, user); CheckHandler.OnQuestRemoved(quest);
        CheckHandler.OnArtifactDiscovered(DewSave.profileMain, keys[0], false);
        Check(!CheckHandler.IsArtifactDiscovered(DewSave.profileMain, keys[0]) && DewSave.saves == writes,
            "Transient profiles cannot receive pilgrimage checks");
    }
}
