"""Generate apworld/shape_of_dreams/data/game_data.json from the game's RawData plus the hand-maintained tables below.

The JSON is the single source of truth for item/location names, keys and IDs. The apworld (Python) reads it, and
the client mod (C#) embeds it, so both sides always agree. DESIGN.md describes the model this file implements.

IDs are stable: every key->id assignment ever made is kept in the JSON's "id_history" (including keys the game has since
removed), so a key always keeps its ID, a removed key that comes back gets its old ID again, and a retired ID is never
given to anything else. Only new keys get fresh IDs, above every ID in the history. Never renumber by hand.
`--renumber` throws the history away; only use it before the first release.

Usage:
    python tools/extract_game_data.py [--game-dir "C:/.../Shape of Dreams"] [--renumber]
The game dir falls back to $SOD_GAME_DIR, then the default Steam path.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
import re
import sys
from collections import Counter
from pathlib import Path

DEFAULT_GAME_DIR = r"C:\Program Files (x86)\Steam\steamapps\common\Shape of Dreams"
REPO_ROOT = Path(__file__).resolve().parent.parent
DATA_DIR = REPO_ROOT / "apworld" / "shape_of_dreams" / "data"
OUT_PATH = DATA_DIR / "game_data.json"

# Bump when the JSON layout changes in a way the mod has to know about. The mod refuses slot_data from a different
# data version, so a seed generated with one apworld can't silently be played with an incompatible mod. Content changes
# (new achievements after a game update, ...) are caught by "data_hash" instead.
DATA_FORMAT_VERSION = 5

# The apworld's data_hash.py, loaded by path: importing it as a package module would pull in Archipelago.
_spec = importlib.util.spec_from_file_location("sod_data_hash", DATA_DIR / "data_hash.py")
data_hash = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(data_hash)

ITEM_ID_BASE = 7_710_000
LOCATION_ID_BASE = 7_720_000

# DESIGN.md "Alternate memory order": each Traveler's three achievement-locked memories, in the order the progressive
# item unlocks them (Q -> R -> Identity; Bismuth's are all QR). The order is a design decision, not derived.
ALT_MEMORY_ORDER = {
    "Hero_Aurena": ["St_Q_Reduction", "St_R_ChainReaction", "St_D_BeautifulThreat"],
    "Hero_Bismuth": ["St_QR_DistortedMind", "St_QR_InfernalTales", "St_QR_ValiantHeart"],
    "Hero_Cetus": ["St_Q_BigBorealChunk", "St_R_FrozenFists", "St_D_ChargedAnguillian"],
    "Hero_Husk": ["St_Q_DeathMark", "St_R_Deception", "St_D_ScarOfTheWind"],
    "Hero_Lacerta": ["St_Q_IncendiaryRounds", "St_R_PrecisionShot", "St_D_DoubleTap"],
    "Hero_Mist": ["St_Q_Fleche", "St_R_Parry", "St_D_AstridsMasterpiecePriorite"],
    "Hero_Nachia": ["St_Q_MoonlightPact", "St_R_SerpentineBlessing", "St_D_CircleOfLife"],
    "Hero_Vesper": ["St_Q_Discipline", "St_R_BaptismOfSun", "St_D_MercyOfEl"],
    "Hero_Yubar": ["St_Q_SuperNova", "St_R_Tranquility", "St_D_ConvergencePoint"],
}
SLOT_ORDER = {"Q": 0, "QR": 0, "R": 1, "Identity": 2}

# Achievements that can only be completed while playing a specific Traveler. Hand-maintained (DESIGN.md "Logic").
# Cross-checked below against "as/with/using <Traveler>" in the English description, and against the Hero_* types each
# ACH_* class references in Dew.Contents (checked 2026-09-27: the same 27, plus the unused
# ACH_ANGER_MANAGEMENT_PROFESSIONAL, which isn't in RawData and so isn't a location).
ACHIEVEMENT_TRAVELER = {
    "ACH_DECEIVING_LOOKS": "Hero_Aurena",
    "ACH_IMMORTALITY_ACHIEVED": "Hero_Aurena",
    "ACH_OVERFLOWING_HEALS": "Hero_Aurena",
    "ACH_DOUBLE_VISION": "Hero_Bismuth",
    "ACH_LIKE_A_BREEZE": "Hero_Bismuth",
    "ACH_LOOK_RAINBOW": "Hero_Bismuth",
    "ACH_FROM_ABYSS_TO_SUMMIT": "Hero_Cetus",
    "ACH_ICEBERG_THE_GREAT_COMMUNICATOR": "Hero_Cetus",
    "ACH_THE_CHILL_WASHES_AWAY_SINS": "Hero_Cetus",
    "ACH_DOLL_OF_MANA": "Hero_Husk",
    "ACH_HOME_SWEET_HOME": "Hero_Husk",
    "ACH_I_AM_SHELL": "Hero_Husk",
    "ACH_BFR_9000": "Hero_Lacerta",
    "ACH_JUST_LIKE_THE_OLD_DAYS": "Hero_Lacerta",
    "ACH_UNTOUCHABLE": "Hero_Lacerta",
    "ACH_GO_WITH_THE_FLOW": "Hero_Mist",
    "ACH_KING_OF_THE_ELEMENTALS": "Hero_Mist",
    "ACH_VINE_IN_CREVICES": "Hero_Mist",
    "ACH_FULL_MOON": "Hero_Nachia",
    "ACH_SIBLING_FIGHT": "Hero_Nachia",
    "ACH_THE_CYCLE_OF_REBIRTH": "Hero_Nachia",
    "ACH_HOTTER_FIRE_WINS": "Hero_Vesper",
    "ACH_MASTER_OF_HAMMER": "Hero_Vesper",
    "ACH_PEAK_PERFORMANCE": "Hero_Vesper",
    "ACH_NIGHT_OF_COUNTING_THE_STARS": "Hero_Yubar",
    "ACH_ODE_TO_THE_STARS": "Hero_Yubar",
    "ACH_PEACE_AMID_THE_STORM": "Hero_Yubar",
}

# DESIGN.md "Build-Dependent Achievements" location group (they need specific Memories to complete).
BUILD_DEPENDENT_ACHIEVEMENTS = [
    "ACH_FOUR_OF_A_KIND",  # Four of a Kind
    "ACH_FIGHTING_COLD_WITH_COLD",  # Fight Fire with Fire
    "ACH_HOTTER_FIRE_WINS",  # Hotter Fire Wins
    "ACH_OMEGA_POINT",  # Omega Point
    "ACH_WRIST_FRIENDLY_BUILD",  # Wrist-Friendly Build
    "ACH_ME_LOVE_GLOWY_STUFF",  # Twinkle Twinkle
    "ACH_SUPPORT_MAIN",  # Support Specialist
    "ACH_JUST_STAY_STILL",  # Now Now Stay Still
    "ACH_MASTER_OF_MYSTIC_ARTS",  # Master of Mystic Arts
]

# DESIGN.md "Forced Lucid Dreams": each Lucid Dream's LucidDream.type. RawData doesn't have it (it's a prefab field), so
# it was read from the game's asset bundle on 2026-09-28 (game r.1.4.0.13_s). Only Evil and Chaotic dreams can be forced.
LUCID_DREAM_TYPE = {
    "LucidDream_BlandStarSoup": "good",
    "LucidDream_BonVoyage": "good",
    "LucidDream_FalseLifeline": "good",
    "LucidDream_KindArmadillo": "good",
    "LucidDream_FishScales": "evil",
    "LucidDream_GrievousWounds": "evil",
    "LucidDream_MadLife": "evil",
    "LucidDream_MarshOfDestiny": "evil",
    "LucidDream_Overpopulation": "evil",
    "LucidDream_PrudentJellyfish": "evil",
    "LucidDream_EmbraceMortality": "chaotic",
    "LucidDream_HarmlessWhispers": "chaotic",
    "LucidDream_SparklingDreamFlask": "chaotic",
    "LucidDream_TheDarkestUrge": "chaotic",
    "LucidDream_WILD": "chaotic",
}

# DESIGN.md "Souvenirs": the cosmetic accessories the lizard shop sells (each one is an excluded location). RawData has
# no accessories, so they were read from the game's asset bundles on 2026-10-03 (game r.1.4.0.13_s): every Accessory
# MonoBehaviour in the defaultlocalgroup bundle (load the monoscripts bundle with it so m_Script resolves), filtered
# like the shop pool (PropEnt_Merchant_Smoothie.UserCode_TpcPopulateSouvenirs: not generatedFromServer, not
# excludeFromPool). English names are MainLocalization's "<key>_Name" in resources.assets. The mod logs the shop filter
# at startup and warns when it differs from this table.
SOUVENIRS = {
    "Acc_General_HatBDayCake": "Birthday Cake",
    "Acc_General_HatBirdNest": "Small Bird Nest",
    "Acc_General_HatDopeGlasses": "Perfectly Cool Glasses",
    "Acc_General_HatFoxtail": "Foxtail",
    "Acc_General_HatLittleBaam": "Adventuring Baam",
    "Acc_General_HatMushroom": "Mushroom Cluster",
    "Acc_General_HatPerfectTurkey": "Perfect Turkey",
    "Acc_General_HatSproutOfConcept": "Growing Sprout",
    "Acc_General_PlushAurena": "Mini Aurena",
    "Acc_General_PlushBismuth": "Mini Bismuth",
    "Acc_General_PlushCetus": "Mini Cetus",
    "Acc_General_PlushHusk": "Mini Shell",
    "Acc_General_PlushLacerta": "Mini Lacerta",
    "Acc_General_PlushMist": "Mini Mist",
    "Acc_General_PlushNachia": "Mini Nachia",
    "Acc_General_PlushVesper": "Mini Vesper",
    "Acc_General_PlushYubar": "Mini Yubar",
}

# DESIGN.md "Pilgrimage checks" (issue #9). Leave out side content whose purpose an achievement already covers.
SHRINES = {
    "Shrine_PotOfGreed": "Pot of Greed",
    "Shrine_MawOfDoom": "Maw of Doom",
    "Shrine_Hatred": "Hatred",
    "Shrine_Paradox": "Paradox",
    "Shrine_MirrorOfRemorse": "Mirror of Remorse",
    "Shrine_Destiny": "Destiny",
    "Shrine_Entanglement": "Entanglement",
    "Shrine_AltarOfCleansing": "Altar of Cleansing",
    "Shrine_Ascension": "Ascension",
}
QUESTS = {
    "Quest_StrayMemory": "Stray Memory",
    "Quest_StarSeekersJournal": "Star Seeker's Journal",
    "Quest_FragmentOfRadiance": "Fragment of Radiance",
    "Quest_CallOfTheRavenous": "Call of the Ravenous",
    "Quest_TheConsortOfNight": "Consort of Night",
    "Quest_HuntedByObliviax": "Hunted by Obliviax",
}
# The twelve non-excluded artifact prefabs listed in issue #9; English names from MainLocalization.
# The journal's Complete flag means handed in to the Dream Teller, not merely picked up.
ARTIFACTS = {
    "Artifact_BouquetOfEyes": "Bouquet of Eyes",
    "Artifact_EmblemOfSubjugation": "Emblem of Subjugation",
    "Artifact_FirstMerchantsToken": "First Merchant's Token",
    "Artifact_FoolsGold": "Fool's Gold",
    "Artifact_ForestHoundSeed": "Forest Hound Seed",
    "Artifact_NightmareCatalyst": "Nightmare Catalyst",
    "Artifact_StarBlossom": "Star Blossom",
    "Artifact_TheStarlitStone": "The Starlit Stone",
    "Artifact_TomeOfTheSeeker": "Tome of the Seeker",
    "Artifact_VoidWhisperer": "Void Whisperer",
    "Artifact_WatchersNote": "Watcher's Note",
    "Artifact_WeddingRing": "Wedding Ring",
}

# Difficulties, easiest first. "rank" orders them for "this difficulty or harder". "game_id" is the
# GameSettingsManager.difficulty / DewGameResult.difficulty string. The ids come from the asset catalog; the mapping to
# display names is ordered by the in-game list and is logged by the mod at startup ("[AP] Difficulty ...") so it can be
# confirmed in-game. Nap has no locations.
DIFFICULTIES = [
    {"key": "NAP", "name": "Nap", "game_id": "diffEasy", "rank": 0, "has_locations": False},
    {"key": "DEEP_SLEEP", "name": "Deep Sleep", "game_id": "diffNormal", "rank": 1, "has_locations": True},
    {"key": "OMINOUS_DREAM", "name": "Ominous Dream", "game_id": "diffHard", "rank": 2, "has_locations": True},
    {"key": "NIGHTMARE", "name": "Nightmare", "game_id": "diffNightmare", "rank": 3, "has_locations": True},
]
WORLDS = [1, 2, 3, 4, 5]

# DESIGN.md "Logic": copies of the Traveler's progressive item a world clear needs (the first copy is the Traveler).
CLEAR_COPIES_REQUIRED = {"DEEP_SLEEP": 1, "OMINOUS_DREAM": 2, "NIGHTMARE": 3}

STARDUST_KEY = "STARDUST"

# Issue #7: table order breaks largest-remainder ties. Delivery is implemented by #10–#12.
# Synthetic blessing keys distinguish map placement from the room modifiers themselves.
BLESSINGS = [
    ("Pure Dream", "RoomMod_PureDream", 2),
    ("Gold Everywhere", "RoomMod_GoldEverywhere", 2),
    ("Harder Fight, Better Reward", "RoomMod_HarderFightBetterReward", 2),
    *[(name, "RoomMod_Spawn" + suffix, 3) for name, suffix in [
        ("Blessed Guidance", "BlessedGuidance"), ("Pot of Greed", "PotOfGreed"),
        ("Maw of Doom", "MawOfDoom"), ("Hatred", "Hatred"), ("Paradox", "Paradox"),
        ("Mirror of Remorse", "MirrorOfRemorse"), ("Destiny", "Destiny"),
        ("Entanglement", "Entanglement"), ("Disintegration", "Disintegration"),
        ("Altar of Cleansing", "AltarOfCleansing"),
    ]],
    ("Lizard Shop", "RoomMod_GiftMerchant", 1),
    ("Artifact", "RoomMod_Artifact", 1),
]
TREASURES = [
    ("Totally Genuine Treasure Map", "Treasure_TotallyGenuineTreasureMap", 4),
    ("Treasure Map", "Treasure_TreasureMap", 3),
    ("Determination Shard", "Treasure_FragmentOfDetermination", 3),
    ("Cloak of Guidance", "Treasure_CloakOfGuidance", 2),
    ("Clairvoyance", "Treasure_Clairvoyance", 2),
]
CURSES = [("Mild", 3), ("Potent", 2), ("Intense", 1)]

STAR_CATEGORIES = ("Destruction", "Flexible", "Imagination", "Life")


def split_camel(s: str) -> str:
    words = re.sub(r"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ", s).split(" ")
    return " ".join([words[0]] + [w.lower() if w in ("Of", "The", "And") else w for w in words[1:]])


def load(raw: Path, name: str) -> dict:
    with open(raw / "en-US" / f"{name}.json", encoding="utf-8-sig") as f:
        return json.load(f)


def fail(msg: str) -> None:
    raise SystemExit(f"extract_game_data: {msg}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--game-dir", default=os.environ.get("SOD_GAME_DIR", DEFAULT_GAME_DIR))
    ap.add_argument("--renumber", action="store_true", help="discard existing IDs (only before the first release)")
    args = ap.parse_args()
    raw = Path(args.game_dir) / "RawData"
    if not raw.is_dir():
        print(f"RawData not found under {args.game_dir}", file=sys.stderr)
        return 1

    travelers = load(raw, "travelers")
    memories = load(raw, "memories")
    essences = load(raw, "essences")
    achievements = load(raw, "achievements")
    stars = load(raw, "stars")
    game_version = (Path(args.game_dir) / "version.txt").read_text().strip()

    targets = {a["unlocked"]: key for key, a in achievements.items() if a.get("unlocked")}
    if len(targets) != len(achievements):
        fail("expected every achievement to unlock exactly one distinct target")

    # Travelers, alphabetical by display name.
    trav_order = sorted(travelers, key=lambda k: travelers[k]["name"])
    trav_name = {k: travelers[k]["name"] for k in trav_order}

    # --- Validate the hand tables against RawData --------------------------------------------------------------------
    if set(ALT_MEMORY_ORDER) != set(trav_order):
        fail("ALT_MEMORY_ORDER must list every Traveler")
    for hero, order in ALT_MEMORY_ORDER.items():
        locked = sorted(k for k, m in memories.items() if m.get("traveler") == hero and k in targets)
        if sorted(order) != locked:
            fail(f"{hero}: alternate memories {order} != achievement-locked memories {locked}")
        slots = [SLOT_ORDER[memories[k]["travelerMemoryLocation"]] for k in order]
        if slots != sorted(slots):
            fail(f"{hero}: alternate memory order is not Q -> R -> Identity")

    by_name = {n.lower(): k for k, n in trav_name.items()}
    pattern = re.compile(r"\b(?:as|with|using)\s+(" + "|".join(re.escape(n) for n in by_name) + r")\b", re.I)
    for ach_key, ach in achievements.items():
        m = pattern.search(ach["description"])
        parsed = by_name[m.group(1).lower()] if m else None
        if parsed != ACHIEVEMENT_TRAVELER.get(ach_key):
            fail(f"{ach_key}: table says {ACHIEVEMENT_TRAVELER.get(ach_key)}, description says {parsed}")
    for key in list(ACHIEVEMENT_TRAVELER) + BUILD_DEPENDENT_ACHIEVEMENTS:
        if key not in achievements:
            fail(f"{key} is not in achievements.json")
    lucid_targets = sorted(k for k in targets if k.startswith("LucidDream_"))
    if sorted(LUCID_DREAM_TYPE) != lucid_targets:
        fail(f"LUCID_DREAM_TYPE must list exactly the Lucid Dreams: {lucid_targets}")

    # --- Items -------------------------------------------------------------------------------------------------------
    items: list[dict] = []
    for hero in trav_order:
        # Every Traveler is an unlock, including vanilla's starting ones: each seed precollects 2 random Travelers.
        unlocks = [hero] + ALT_MEMORY_ORDER[hero]
        unlock_names = [trav_name[k] if k in travelers else memories[k]["name"] for k in unlocks]
        items.append({"name": f"Progressive {trav_name[hero]}", "key": f"PROGRESSIVE_{hero}", "kind": "progressive",
                      "classification": "progression", "traveler": hero, "unlocks": unlocks,
                      "unlock_names": unlock_names})
    alt_memories = {k for order in ALT_MEMORY_ORDER.values() for k in order}
    general = sorted((k for k in targets if k.startswith("St_") and k not in alt_memories),
                     key=lambda k: memories[k]["name"])
    for key in general:
        items.append({"name": f"Memory: {memories[key]['name']}", "key": key, "kind": "memory",
                      "classification": "useful", "unlocks": [key]})
    for key in sorted((k for k in targets if k.startswith("Gem_")), key=lambda k: essences[k]["name"]):
        items.append({"name": f"Essence: {essences[key]['name']}", "key": key, "kind": "essence",
                      "classification": "useful", "unlocks": [key]})
    # RawData has no Lucid Dream names, so derive them from the type name.
    # "lucid_dream_type" only decides which forced-dream option accepts the dream; it isn't in data_hash, because the
    # seed's slot_data carries the resolved keys.
    for key in lucid_targets:
        items.append({"name": f"Lucid Dream: {split_camel(key[len('LucidDream_'):])}", "key": key,
                      "kind": "lucid_dream", "classification": "useful", "unlocks": [key],
                      "lucid_dream_type": LUCID_DREAM_TYPE[key]})
    for hero in trav_order:
        items.append({"name": f"Mastery: {trav_name[hero]}", "key": f"MASTERY_{hero}", "kind": "mastery",
                      "classification": "useful", "traveler": hero})
    items.append({"name": "Stardust", "key": STARDUST_KEY, "kind": "stardust", "classification": "filler"})
    for name, modifier, weight in BLESSINGS:
        items.append({"name": f"Blessing: {name}", "key": "BLESSING_" + modifier, "kind": "blessing",
                      "classification": "filler", "target": modifier, "weight": weight})
    for name, prefab, weight in TREASURES:
        items.append({"name": name, "key": prefab, "kind": "treasure", "classification": "filler",
                      "target": prefab, "weight": weight})
    for strength, weight in CURSES:
        items.append({"name": f"Curse: {strength}", "key": "CURSE_" + strength.upper(), "kind": "curse",
                      "classification": "trap", "target": strength, "weight": weight})

    covered = Counter(u for i in items for u in i.get("unlocks", []))
    if covered != Counter(set(targets) | set(trav_order)):
        fail("unlock items don't cover every achievement target and every Traveler exactly once")

    # --- Locations ---------------------------------------------------------------------------------------------------
    locations: list[dict] = []
    for ach_key in sorted(achievements):
        locations.append({"name": f"Achievement: {achievements[ach_key]['name']}", "key": ach_key,
                          "kind": "achievement", "traveler": ACHIEVEMENT_TRAVELER.get(ach_key),
                          "build_dependent": ach_key in BUILD_DEPENDENT_ACHIEVEMENTS,
                          # Display only (Universal Tracker shows it next to the name), so not in data_hash.
                          "description": achievements[ach_key]["description"].strip().rstrip(".")})
    clear_diffs = [d for d in DIFFICULTIES if d["has_locations"]]
    for hero in trav_order:
        for world in WORLDS:
            for diff in clear_diffs:
                req = CLEAR_COPIES_REQUIRED[diff["key"]]
                locations.append({"name": f"World {world} Clear ({diff['name']}): {trav_name[hero]}",
                                  "key": f"CLEAR_W{world}_{diff['key']}_{hero}", "kind": "world_clear",
                                  "traveler": hero, "world": world, "difficulty": diff["key"],
                                  "copies_required": req})
    for key in sorted(SOUVENIRS, key=SOUVENIRS.get):
        locations.append({"name": f"Souvenir: {SOUVENIRS[key]}", "key": key, "kind": "souvenir", "traveler": None})

    # All possible wares have stable IDs; each seed enables only numbers <= jonas_wares.
    for number in range(1, 101):
        locations.append({"name": f"Jonas's Ware {number}", "key": f"WARE_JONAS_{number}",
                          "kind": "ware", "traveler": None, "number": number})

    for kind, table in (("shrine", SHRINES), ("quest", QUESTS), ("artifact", ARTIFACTS)):
        for key, name in table.items():
            locations.append({"name": f"{kind.title()}: {name}", "key": key, "kind": kind, "traveler": None})

    for kind, entries in (("item", items), ("location", locations)):
        for field in ("name", "key"):
            values = [e[field] for e in entries]
            dupes = {v for v in values if values.count(v) > 1}
            if dupes:
                fail(f"duplicate {kind} {field}s: {sorted(dupes)}")

    # --- Location groups (DESIGN.md "Locations") ---------------------------------------------------------------------
    groups: dict[str, list[str]] = {
        "Achievements": [l["name"] for l in locations if l["kind"] == "achievement"],
        "World Clears": [l["name"] for l in locations if l["kind"] == "world_clear"],
        "Build-Dependent Achievements": [l["name"] for l in locations if l.get("build_dependent")],
        "Souvenirs": [l["name"] for l in locations if l["kind"] == "souvenir"],
        "Jonas's Wares": [l["name"] for l in locations if l["kind"] == "ware"],
        "Shrines": [l["name"] for l in locations if l["kind"] == "shrine"],
        "Quests": [l["name"] for l in locations if l["kind"] == "quest"],
        "Artifacts": [l["name"] for l in locations if l["kind"] == "artifact"],
    }
    for hero in trav_order:
        groups[trav_name[hero]] = [l["name"] for l in locations if l["traveler"] == hero]
    for diff in clear_diffs:
        groups[diff["name"]] = [l["name"] for l in locations if l.get("difficulty") == diff["key"]]

    # --- Stars (DESIGN.md "Shuffled star requirements") -------------------------------------------------------------
    # Not items or locations: the generator only shuffles their mastery requirements, and slot_data carries the result
    # by key, so none of this is in data_hash. "traveler" is null for the common stars (they need total mastery).
    star_list: list[dict] = []
    for key in sorted(stars):
        st = stars[key]
        hero = st["heroType"] or None
        if hero is not None and hero not in travelers:
            fail(f"{key}: unknown Traveler {hero}")
        if st["isRequiredLevelTotalMastery"] != (hero is None):
            fail(f"{key}: expected Traveler stars to need Traveler mastery and common stars total mastery")
        if st["category"] not in STAR_CATEGORIES:
            fail(f"{key}: unknown star category {st['category']}")
        star_list.append({"key": key, "traveler": hero, "category": st["category"],
                          "required_level": st["requiredLevel"]})

    # --- IDs ---------------------------------------------------------------------------------------------------------
    # id_history holds every key->id ever assigned. Older files without it: rebuild it from their live entries.
    history: dict[str, dict[str, int]] = {"items": {}, "locations": {}}
    if OUT_PATH.exists() and not args.renumber:
        old = json.loads(OUT_PATH.read_text(encoding="utf-8"))
        for section in history:
            history[section] = dict(old.get("id_history", {}).get(section, {}))
            for e in old.get(section, []):
                history[section].setdefault(e["key"], e["id"])
    for section, entries, base in (("items", items, ITEM_ID_BASE), ("locations", locations, LOCATION_ID_BASE)):
        known = history[section]
        if len(set(known.values())) != len(known):
            fail(f"id_history.{section} assigns one ID to several keys")
        next_id = max(known.values(), default=base) + 1
        for e in entries:
            if e["key"] in known:
                e["id"] = known[e["key"]]
            else:
                e["id"] = known[e["key"]] = next_id
                next_id += 1
    retired = {s: sorted(set(history[s]) - {e["key"] for e in entries})
               for s, entries in (("items", items), ("locations", locations))}

    out = {
        "game": "Shape of Dreams",
        "data_format_version": DATA_FORMAT_VERSION,
        "data_hash": "",
        "extracted_from_game_version": game_version,
        "difficulties": DIFFICULTIES,
        "travelers": [{
            "key": hero,
            "name": trav_name[hero],
            "progressive_item": f"Progressive {trav_name[hero]}",
            "mastery_item": f"Mastery: {trav_name[hero]}",
            # Every Q/R/Identity memory of this Traveler (base and alternate). The mod protects the lock status of
            # these, plus every unlock target, from the game's own re-locking on marked profiles.
            "skills": sorted(k for k, m in memories.items()
                             if m.get("traveler") == hero and m.get("travelerMemoryLocation") in SLOT_ORDER),
        } for hero in trav_order],
        "items": sorted(items, key=lambda e: e["id"]),
        "locations": sorted(locations, key=lambda e: e["id"]),
        "location_groups": groups,
        "stars": star_list,
        # Every key->id ever assigned, live or retired. Never edit by hand; see the module docstring.
        "id_history": {s: dict(sorted(history[s].items(), key=lambda kv: kv[1])) for s in history},
    }
    out["data_hash"] = data_hash.compute(out)
    for section, keys in retired.items():
        if keys:
            print(f"note: retired {section} (kept in id_history): {', '.join(keys)}")
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    counts = {k: sum(1 for i in items if i["kind"] == k) for k in dict.fromkeys(i["kind"] for i in items)}
    loc_counts = {k: sum(1 for l in locations if l["kind"] == k) for k in dict.fromkeys(l["kind"] for l in locations)}
    print(f"wrote {OUT_PATH.relative_to(REPO_ROOT)}: items {counts}, locations {loc_counts}, stars {len(star_list)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
