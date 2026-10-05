using System;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Models;

namespace UnityEngine
{
    public class Object { public static void Destroy(Object obj) { } }
    public class Texture2D : Object
    {
        public int width, height;
        public Texture2D(int width, int height) { this.width = width; this.height = height; }
        public void LoadImage(byte[] bytes) { }
    }
    public class Sprite : Object
    {
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => new Sprite();
    }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public struct Vector2 { public Vector2(float x, float y) { } }
    public class Transform { public Vector3 position; }
    public static class Debug
    {
        public static void Log(string text) { }
        public static void LogWarning(string text) { }
        public static void LogException(Exception exception) { }
    }
    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }
    public struct Vector3 { public float x, y, z; }
    public struct Quaternion { }
    public static class Random
    {
        public static float value = 0f;
        public static int Range(int min, int max) => max - 1;
    }
}
namespace UnityEngine.UI
{
    public class Image { public UnityEngine.Sprite sprite; }
    public class Button { public bool interactable; }
}
public class Label { public string text; }
public class CostDisplay { public Cost cost; public void Setup(Cost value) { cost = value; } }
public class UI_InGame_FloatingWindow_Shop_Item
{
    public UnityEngine.UI.Image treasureIcon = new UnityEngine.UI.Image();
    public CostDisplay costDisplay = new CostDisplay();
    public Label quantityText = new Label();
    public UnityEngine.UI.Button button = new UnityEngine.UI.Button();
    public UnityEngine.Transform transform = new UnityEngine.Transform();
    public MerchandiseData data;
    public T GetComponent<T>() where T : class => button as T;
}
public class UI_TooltipManager
{
    public string text;
    public void ShowRawTextTooltip(UnityEngine.Vector3 position, string value) { text = value; }
}
public class FloatingWindowManager { public object currentTarget; }
public class ManagerBase<T> { public static T softInstance; }
namespace Mirror
{
    public static class NetworkServer
    {
        public static bool active;
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new Dictionary<uint, NetworkIdentity>();
    }
    public class NetworkIdentity
    {
        public Actor actor;
        public bool TryGetComponent<T>(out T component) where T : class
        {
            component = actor as T; return component != null;
        }
    }
}
namespace Archipelago.MultiClient.Net.Models { public class ItemInfo { public long ItemId, LocationId; } }

public class NetworkedManagerBase<T>
{
    private static T _instance;
    public static int instanceLookups;
    public static T instance
    {
        get { instanceLookups++; return _instance; }
        set { _instance = value; }
    }
    public static T softInstance => _instance;
}
public class SingletonBehaviour<T> { public static T instance; }
public class UI_Constellations { public State state = new State(); public class State { public int stardust; } }
public static class Dew
{
    public static Func<string, bool> curseIncluded = name => true;
    public static bool IsCurseIncludedInGame(string name) => curseIncluded(name);
    public static readonly List<(CurseStatusEffect curse, float weight)> weightedCurses = new List<(CurseStatusEffect, float)>();
    public static T SelectRandomWeightedInList<T>(IList<T> list, Func<T, float> weightGetter)
    {
        weightedCurses.Clear();
        foreach (var value in list) weightedCurses.Add(((CurseStatusEffect)(object)value, weightGetter(value)));
        return list[list.Count - 1];
    }
    public static int GetRequiredMasteryPointsToLevelUp(int level) => 100;
    public static readonly List<Treasure> spawned = new List<Treasure>();
    public static bool deferTreasureCreate;
    public static T InstantiateAndSpawn<T>(T prefab, UnityEngine.Vector3 position, UnityEngine.Quaternion? rotation,
        Action<T> beforeSpawn = null) where T : Treasure
    {
        var instance = (T)Activator.CreateInstance(prefab.GetType());
        instance.position = position; instance.price = 999; instance.merchant = new PropEnt_Merchant_Base();
        instance.customData = "prefab data"; instance.throwOnDestroy = prefab.throwOnDestroy;
        instance.onCreate = () => prefab.onSpawn?.Invoke(instance);
        beforeSpawn?.Invoke(instance);
        spawned.Add(instance);
        Mirror.NetworkServer.spawned[(uint)Mirror.NetworkServer.spawned.Count + 1] =
            new Mirror.NetworkIdentity { actor = instance };
        if (!deferTreasureCreate) instance.CompleteCreate();
        return instance;
    }
}
public class GameManager
{
    public bool isGameConcluded;
    public bool ready = true;
    public bool IsLazyCallReady() => ready;
}
public class ZoneManager
{
    public Zone currentZone = new Zone();
    public Room currentRoom = new Room();
    public int currentNodeIndex;
    public bool isInRoomTransition;
    public bool isSidetracking;
    public WorldNodeData currentNode => nodes[currentNodeIndex];
    public int currentZoneIndex, loopIndex;
    public bool isHuntAdvanceDisabled;
    public int selectedNode = 2;
    public readonly List<WorldNodeData> nodes = new List<WorldNodeData> { new WorldNodeData(), new WorldNodeData(), new WorldNodeData() };
    public readonly List<GetNodeIndexSettings> searches = new List<GetNodeIndexSettings>();
    public readonly List<(int node, ModifierData mod)> additions = new List<(int, ModifierData)>();
    public Func<GetNodeIndexSettings, bool> canSelect = settings => true;
    public bool TryGetNodeIndexForNextGoal(GetNodeIndexSettings settings, out int node)
    {
        searches.Add(settings); node = selectedNode; return canSelect(settings);
    }
    public int AddModifier(int node, ModifierData mod)
    {
        additions.Add((node, mod)); nodes[node].modifiers.Add(mod); return additions.Count;
    }
}
public class GetNodeIndexSettings
{
    public WorldNodeType[] allowedTypes = new[] { WorldNodeType.Combat };
    public UnityEngine.Vector2Int desiredDistance;
    public bool preferCloserToExit, avoidMainModifier;
}
public class Room { public string name = "Room_Combat"; }
public class Zone { public bool useSpecialGeneration; }
public class WorldNodeData
{
    public WorldNodeType type;
    public readonly List<ModifierData> modifiers = new List<ModifierData>();
    public bool HasModifier(string name) => modifiers.Exists(m => m.type == name);
}
public enum WorldNodeType { Combat, ExitBoss, Special, Merchant }
public struct ModifierData { public string type; public bool isForceRevealed; }
public class RoomModifierBase { public bool isMain; }
public class PingManager
{
    public enum PingType { WorldNode }
    public struct Ping { public DewPlayer sender; public PingType type; public int itemIndex; }
    public readonly List<Ping> pings = new List<Ping>();
    public bool throwOnPing;
    public void BroadcastPing(Ping ping)
    {
        if (throwOnPing) throw new Exception("Feedback failure");
        pings.Add(ping);
    }
}
public class GameSettingsManager { public string difficulty; }
public class Actor { public bool isActive = true; }
public class ActorManager { public readonly HashSet<Actor> allActors = new HashSet<Actor>(); }
public class Entity : Actor { public DewPlayer owner; }
public class Hero : Entity
{
    public bool isKnockedOut;
    public UnityEngine.Vector3 agentPosition = new UnityEngine.Vector3 { x = 1, y = 2, z = 3 };
    public EntityStatus Status = new EntityStatus();
    public int kills;
    public Action onKill;
    public void Kill() { kills++; onKill?.Invoke(); }
    public readonly List<CurseStatusEffect> curses = new List<CurseStatusEffect>();
    public bool rejectCurse;
    public T CreateStatusEffect<T>(T prefab, Entity victim, CastInfo info, Action<T> beforePrepare) where T : CurseStatusEffect
    {
        if (rejectCurse) return null;
        var curse = (T)new CurseStatusEffect { parent = this, victim = victim, info = info, source = prefab };
        beforePrepare(curse);
        curses.Add(curse);
        return curse;
    }
}
public static class EntityCheck
{
    public static bool IsNullInactiveDeadOrKnockedOut(this Hero hero) =>
        hero == null || !hero.isActive || hero.Status.isDead || hero.isKnockedOut;
}
public class EntityStatus
{
    public bool isDead;
    public readonly HashSet<Type> effects = new HashSet<Type>();
    public bool HasStatusEffect<T>() => effects.Contains(typeof(T));
}
public class Se_HeroKnockedOut { }
public class Se_HeroBleedingOut { }
[Flags] public enum HatredStrengthType { None = 0, Mild = 1, Potent = 2, Powerful = 4 }
public enum QuestProgressType { Kills, Travel }
public struct CastInfo
{
    public Entity caster, target;
    public CastInfo(Entity caster, Entity target) { this.caster = caster; this.target = target; }
}
public class CurseStatusEffect
{
    public HatredStrengthType availableStrengths = HatredStrengthType.Mild | HatredStrengthType.Potent | HatredStrengthType.Powerful;
    public HatredStrengthType currentStrength;
    public float chanceWeight = 1f;
    public int skillLevel, requiredAmount;
    public QuestProgressType progressType;
    public Entity parent, victim;
    public CastInfo info;
    public CurseStatusEffect source;
    public Func<Entity, bool> viable = hero => true;
    public virtual bool IsViable(Entity hero) => viable(hero);
}
public struct ResourceLoadSettings { public static readonly ResourceLoadSettings Light = new ResourceLoadSettings(); }
public struct AssetRef<T>
{
    private readonly T _asset;
    public AssetRef(T asset) { _asset = asset; }
    public T asset => DewResources.fullCurses.TryGetValue((CurseStatusEffect)(object)_asset, out var full)
        ? (T)(object)full : _asset;
}
public class QuestManager { public string currentArtifact; public bool didCollectArtifactThisLoop; }
public class Shrine { }
public class Shrine_PotOfGreed : Shrine { }
public class Shrine_Disintegration : Shrine { }
public enum QuestState { Ongoing, Completed, Failed }
public class DewQuest : Actor { public QuestState state; }
public class Quest_StrayMemory : DewQuest { }
public class Quest_GuidingCompass : DewQuest { }
public class Quest_TreasureMap : DewQuest { }
public class Quest_SuspiciousTreasureMap : DewQuest { }
public enum UnlockStatus { Locked, NotDiscovered, Complete }
public class DewPlayer { public static DewPlayer local; public Hero hero; public string guid; public float buyPriceMultiplier = 1f; }
public enum MerchandiseType { Empty, Skill, Gem, Souvenir, Treasure }
public struct Cost
{
    public int gold;
    public Cost MultiplyGold(float multiplier) => new Cost { gold = (int)(gold * multiplier) };
}
public struct MerchandiseData { public MerchandiseType type; public string itemName, customData; public Cost price; public int count; }
public class PropEnt_Merchant_Base
{
    public readonly Dictionary<string, MerchandiseData[]> merchandises = new Dictionary<string, MerchandiseData[]>();
}
public class PropEnt_Merchant_Jonas : PropEnt_Merchant_Base { }
public class Treasure : Actor
{
    public UnityEngine.Sprite icon = new UnityEngine.Sprite();
    public int priceCalls;
    public int price;
    public string customData;
    public PropEnt_Merchant_Base merchant;
    public DewPlayer player;
    public Hero hero;
    public UnityEngine.Vector3 position;
    public bool eligible = true, destroyed, throwOnDestroy;
    public Action<Treasure> onSpawn;
    public Action onCreate;
    public bool created;
    public void CompleteCreate()
    {
        if (created) return;
        created = true; onCreate?.Invoke();
        NetworkedManagerBase<ActorManager>.instance?.allActors.Add(this);
    }
    public bool ShouldBeIncludedInPool() => eligible;
    public void Destroy()
    {
        if (throwOnDestroy) throw new Exception("Cleanup failure");
        CompleteCreate();
        destroyed = true;
        isActive = false;
    }
    public void OnAddMerchandise(out Cost price, out string customData) { priceCalls++; price = new Cost { gold = 237 }; customData = null; }
}
public class Treasure_TreasureMap : Treasure { }
public class Treasure_TotallyGenuineTreasureMap : Treasure { }
public static class DewResources
{
    public static readonly List<CurseStatusEffect> curses = new List<CurseStatusEffect>();
    public static readonly Dictionary<CurseStatusEffect, CurseStatusEffect> fullCurses = new Dictionary<CurseStatusEffect, CurseStatusEffect>();
    public static IEnumerable<T> FindAllByType<T>(ResourceLoadSettings settings) =>
        System.Linq.Enumerable.Cast<T>(curses);
    public static readonly Treasure treasure = new Treasure();
    public static readonly Dictionary<string, RoomModifierBase> modifiers = new Dictionary<string, RoomModifierBase>();
    public static readonly Dictionary<string, Treasure> treasures = new Dictionary<string, Treasure>();
    public static T GetByShortTypeName<T>(string name)
    {
        if (typeof(T) == typeof(Treasure)) return treasures.TryGetValue(name, out var t) ? (T)(object)t : default;
        return modifiers.TryGetValue(name, out var mod) ? (T)(object)mod : default;
    }
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
        public static bool deathLinkEnabled;
        public static bool DeathLinkEnabled => deathLinkEnabled && ProfileGuard.Bound;
        public static readonly List<string> DeathLinks = new List<string>();
        public static void SendDeathLink(string cause) { if (DeathLinkEnabled) DeathLinks.Add(cause); }
        public static string SlotName = "Test";
        public static readonly Dictionary<string, int> SlotData = new Dictionary<string, int>();
        public static readonly List<ItemInfo> ReceivedItems = new List<ItemInfo>();
        public static readonly List<string> Notices = new List<string>();
        public static readonly List<long> Sent = new List<long>();
        public static readonly HashSet<long> CheckedLocations = new HashSet<long>();
        public static bool HasCheckedLocation(long id) => ProfileGuard.Bound && IsConnected && CheckedLocations.Contains(id);
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
            public string Name, Key, Kind, Target;
            public List<string> Unlocks = new List<string>();
            public List<string> UnlockNames;
        }
        public class Traveler { public string Key, Name; }
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
