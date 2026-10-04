using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Models;

namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(string text) { }
        public static void LogWarning(string text) { }
    }
    public static class Random { public static int Range(int min, int max) => max - 1; }
}
namespace Mirror { public static class NetworkServer { public static bool active; } }
namespace Archipelago.MultiClient.Net.Models { public class ItemInfo { public long ItemId, LocationId; } }

public class NetworkedManagerBase<T> { public static T instance; }
public class SingletonBehaviour<T> { public static T instance; }
public class UI_Constellations { public State state = new State(); public class State { public int stardust; } }
public static class Dew { public static int GetRequiredMasteryPointsToLevelUp(int level) => 100; }
public class GameManager
{
    public bool isGameConcluded;
    public bool ready = true;
    public bool IsLazyCallReady() => ready;
}
public class ZoneManager { public object currentZone = new object(); public int currentZoneIndex, loopIndex; }
public class GameSettingsManager { public string difficulty; }
public class Entity { public DewPlayer owner; }
public class Hero : Entity { }
public class Shrine { }
public class Shrine_PotOfGreed : Shrine { }
public class Shrine_Disintegration : Shrine { }
public enum QuestState { Ongoing, Completed, Failed }
public class DewQuest { public QuestState state; }
public class Quest_StrayMemory : DewQuest { }
public class Quest_GuidingCompass : DewQuest { }
public enum UnlockStatus { Locked, NotDiscovered, Complete }
public class DewPlayer { public static DewPlayer local; public Hero hero; public string guid; }
public enum MerchandiseType { Empty, Skill, Gem, Souvenir, Treasure }
public struct Cost { public int gold; }
public struct MerchandiseData { public MerchandiseType type; public string itemName, customData; public Cost price; public int count; }
public class PropEnt_Merchant_Base
{
    public readonly Dictionary<string, MerchandiseData[]> merchandises = new Dictionary<string, MerchandiseData[]>();
}
public class PropEnt_Merchant_Jonas : PropEnt_Merchant_Base { }
public class Treasure
{
    public int priceCalls;
    public void OnAddMerchandise(out Cost price, out string customData) { priceCalls++; price = new Cost { gold = 237 }; customData = null; }
}
public static class DewResources
{
    public static readonly Treasure treasure = new Treasure();
    public static T GetByShortTypeName<T>(string name) => (T)(object)treasure;
}
public class EventInfoLoadZone { public string from, to; public bool isTraveling, isLoadingFromSave; }
public class DewProfile
{
    public string name = "Test";
    public List<string> experienceFlags = new List<string>();
    public List<object> lastGameResults = new List<object>(), recentlyConcededGames = new List<object>();
    public float totalPlayTimeMinutes;
    public bool didPlayTutorial;
    public int stardust;
    public Dictionary<string, Achievement> achievements = new Dictionary<string, Achievement>();
    public Dictionary<string, Accessory> accessories = new Dictionary<string, Accessory>();
    public Dictionary<string, Artifact> artifacts = new Dictionary<string, Artifact>();
    public class Achievement { public bool isCompleted; }
    public class Accessory { public bool isUnlocked; }
    public class Artifact { public UnlockStatus status; }
}
public class DewProfileStats
{
    public List<string> recoveredLossPoints = new List<string>();
    public Dictionary<string, Stats> heroes = new Dictionary<string, Stats>();
    public class Stats { public long playCount; public int masteryLevel, currentMasteryPoints; }
    public void Validate() { }
    public void AddMasteryPoints(string key, long points) { }
}
public static class DewSave
{
    public static DewProfile profileMain;
    public static DewProfileStats profileStats;
    public static string profileMainPath;
    public static int saves;
    public static void SaveProfileMain(bool immediate = false) { saves++; }
    public static void SaveProfileStats(bool immediate = false) { }
}
namespace SoDArchipelago
{
    public static class UnlockState { public static int Enforce(DewProfile profile, string why) => 0; }
    public static class ForcedDreams { public static bool RunCounts(string why) => true; }
    public static class Diagnostics { public static string DifficultyName(string difficulty) => difficulty; }
    public static class ApClient
    {
        public static bool IsConnected;
        public static string SlotName = "Test";
        public static readonly Dictionary<string, int> SlotData = new Dictionary<string, int>();
        public static readonly List<ItemInfo> ReceivedItems = new List<ItemInfo>();
        public static readonly List<string> Notices = new List<string>();
        public static readonly List<long> Sent = new List<long>();
        public static void Say(string text) => Notices.Add(text);
        public static void ScoutWares(IReadOnlyCollection<GameData.Location> locations) { }
        public static int GetInt(string key, int fallback) => SlotData.TryGetValue(key, out var value) ? value : fallback;
        public static string PlayerName(ItemInfo info) => "Alice";
        public static void SendLocations(ICollection<long> ids)
        {
            if (ProfileGuard.Bound) Sent.AddRange(ids);
        }
    }
    public static class GameData
    {
        public class Item
        {
            public long Id;
            public string Name, Key, Kind;
            public List<string> Unlocks = new List<string>();
            public List<string> UnlockNames;
        }
        public class Traveler { public string Key; }
        public class Location { public long Id; public string Name, Key, Kind; public int Number; }
        public class Difficulty { public string Key; public bool HasLocations; public int Rank; }
        public const int NormalWorlds = 4;
        public const string StardustKey = "STARDUST";
        public static readonly Dictionary<long, Item> ItemsById = new Dictionary<long, Item>();
        public static readonly Dictionary<string, Location> LocationsByKey = new Dictionary<string, Location>();
        public static readonly Dictionary<string, Traveler> Travelers = new Dictionary<string, Traveler>();
        public static readonly List<Location> Souvenirs = new List<Location>();
        public static readonly List<Location> Artifacts = new List<Location>();
        public static readonly List<Difficulty> Difficulties = new List<Difficulty>();
        public static int RankOfGameDifficulty(string id) => -1;
        public static string WorldClearKey(int world, string difficulty, string hero) => "unused";
    }
}
