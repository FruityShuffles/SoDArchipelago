"""Generate apworld/shape_of_dreams/data/game_data.json from the game's RawData plus the hand-maintained tables below.

The JSON is the single source of truth for item/location names, keys and IDs. The apworld (Python) reads it, and
the client mod (C#) embeds it, so both sides always agree. DESIGN.md describes the model this file implements.

IDs are stable: existing key->id assignments in the current game_data.json are kept, and only new entries get fresh
IDs. Never renumber by hand. `--renumber` throws the old IDs away; only use it before the first release.

Usage:
    python tools/extract_game_data.py [--game-dir "C:/.../Shape of Dreams"] [--renumber]
The game dir falls back to $SOD_GAME_DIR, then the default Steam path.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path

DEFAULT_GAME_DIR = r"C:\Program Files (x86)\Steam\steamapps\common\Shape of Dreams"
REPO_ROOT = Path(__file__).resolve().parent.parent
OUT_PATH = REPO_ROOT / "apworld" / "shape_of_dreams" / "data" / "game_data.json"

# Bump when the JSON layout changes in a way the mod has to know about. The mod refuses slot_data from a different
# data version, so a seed generated with one apworld can't silently be played with an incompatible mod.
DATA_FORMAT_VERSION = 2

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

# DESIGN.md "Logic": copies of the Traveler's progressive item a world clear needs, for locked Travelers and for the
# Travelers that start unlocked (Lacerta, Mist). Their first copy is already an alternate memory.
CLEAR_COPIES_REQUIRED = {
    "DEEP_SLEEP": {"locked": 1, "starting": 0},
    "OMINOUS_DREAM": {"locked": 2, "starting": 1},
    "NIGHTMARE": {"locked": 3, "starting": 2},
}

STARDUST_KEY = "STARDUST"


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
    game_version = (Path(args.game_dir) / "version.txt").read_text().strip()

    targets = {a["unlocked"]: key for key, a in achievements.items() if a.get("unlocked")}
    if len(targets) != len(achievements):
        fail("expected every achievement to unlock exactly one distinct target")

    # Travelers, alphabetical by display name. Gated = locked behind an achievement in vanilla.
    trav_order = sorted(travelers, key=lambda k: travelers[k]["name"])
    gated = {k for k in trav_order if k in targets}
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

    # --- Items -------------------------------------------------------------------------------------------------------
    items: list[dict] = []
    for hero in trav_order:
        unlocks = ([hero] if hero in gated else []) + ALT_MEMORY_ORDER[hero]
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
    for key in sorted(k for k in targets if k.startswith("LucidDream_")):
        items.append({"name": f"Lucid Dream: {split_camel(key[len('LucidDream_'):])}", "key": key,
                      "kind": "lucid_dream", "classification": "useful", "unlocks": [key]})
    for hero in trav_order:
        items.append({"name": f"Mastery: {trav_name[hero]}", "key": f"MASTERY_{hero}", "kind": "mastery",
                      "classification": "filler", "traveler": hero})
    items.append({"name": "Stardust", "key": STARDUST_KEY, "kind": "stardust", "classification": "filler"})

    covered = [u for i in items for u in i.get("unlocks", [])]
    if sorted(covered) != sorted(targets):
        fail("unlock items don't cover every achievement target exactly once")

    # --- Locations ---------------------------------------------------------------------------------------------------
    locations: list[dict] = []
    for ach_key in sorted(achievements):
        locations.append({"name": f"Achievement: {achievements[ach_key]['name']}", "key": ach_key,
                          "kind": "achievement", "traveler": ACHIEVEMENT_TRAVELER.get(ach_key),
                          "build_dependent": ach_key in BUILD_DEPENDENT_ACHIEVEMENTS})
    clear_diffs = [d for d in DIFFICULTIES if d["has_locations"]]
    for hero in trav_order:
        for world in WORLDS:
            for diff in clear_diffs:
                req = CLEAR_COPIES_REQUIRED[diff["key"]]["locked" if hero in gated else "starting"]
                locations.append({"name": f"World {world} Clear ({diff['name']}): {trav_name[hero]}",
                                  "key": f"CLEAR_W{world}_{diff['key']}_{hero}", "kind": "world_clear",
                                  "traveler": hero, "world": world, "difficulty": diff["key"],
                                  "copies_required": req})

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
    }
    for hero in trav_order:
        groups[trav_name[hero]] = [l["name"] for l in locations if l["traveler"] == hero]
    for diff in clear_diffs:
        groups[diff["name"]] = [l["name"] for l in locations if l.get("difficulty") == diff["key"]]

    # --- IDs ---------------------------------------------------------------------------------------------------------
    old_ids: dict[str, dict[str, int]] = {"items": {}, "locations": {}}
    if OUT_PATH.exists() and not args.renumber:
        old = json.loads(OUT_PATH.read_text(encoding="utf-8"))
        for section in old_ids:
            old_ids[section] = {e["key"]: e["id"] for e in old.get(section, [])}
    for section, entries, base in (("items", items, ITEM_ID_BASE), ("locations", locations, LOCATION_ID_BASE)):
        known = old_ids[section]
        next_id = max(known.values(), default=base) + 1
        for e in entries:
            if e["key"] in known:
                e["id"] = known[e["key"]]
            else:
                e["id"] = next_id
                next_id += 1

    out = {
        "game": "Shape of Dreams",
        "data_format_version": DATA_FORMAT_VERSION,
        "extracted_from_game_version": game_version,
        "difficulties": DIFFICULTIES,
        "travelers": [{
            "key": hero,
            "name": trav_name[hero],
            "starts_unlocked": hero not in gated,
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
    }
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    counts = {k: sum(1 for i in items if i["kind"] == k) for k in dict.fromkeys(i["kind"] for i in items)}
    loc_counts = {k: sum(1 for l in locations if l["kind"] == k) for k in dict.fromkeys(l["kind"] for l in locations)}
    print(f"wrote {OUT_PATH.relative_to(REPO_ROOT)}: items {counts}, locations {loc_counts}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
