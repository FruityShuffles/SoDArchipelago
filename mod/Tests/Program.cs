using System;
using System.Collections.Generic;
using System.Linq;
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
        ApClient.SlotData.Clear();
        SingletonBehaviour<UI_Constellations>.instance = null;
        Mirror.NetworkServer.active = true;
        NetworkedManagerBase<GameManager>.instance = new GameManager();
        NetworkedManagerBase<ZoneManager>.instance = new ZoneManager();
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
        Stardust(); StardustItems(); Delivery(); Records(); Wares(); Pilgrimage();
        Console.WriteLine($"Passed {_assertions} assertions (Stardust, delivery, checks, wares and pilgrimage).");
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
