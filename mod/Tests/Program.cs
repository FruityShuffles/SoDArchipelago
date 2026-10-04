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
        Stardust(); StardustItems(); Delivery(); Records();
        Console.WriteLine($"Passed {_assertions} assertions (Stardust, delivery and check records).");
    }
}
