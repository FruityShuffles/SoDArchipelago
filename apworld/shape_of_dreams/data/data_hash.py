"""Fingerprint of everything in game_data.json that the mod or the generator acts on (IDs, keys, unlocks, logic data).

tools/extract_game_data.py stores it as "data_hash", the apworld puts it in slot_data, and the mod refuses a seed whose
hash differs from its own embedded copy. Display names, the extracted game version, the ID history and the hash itself
are left out, so renaming something doesn't break compatibility. Pure Python with no Archipelago imports: the extractor
loads this file directly.
"""
import hashlib
import json


def _pick(entry: dict, fields) -> dict:
    return {f: entry.get(f) for f in fields}


def compute(game_data: dict) -> str:
    payload = {
        "items": sorted((_pick(e, ("id", "key", "kind", "classification", "traveler", "unlocks"))
                         for e in game_data["items"]), key=lambda e: e["id"]),
        "locations": sorted((_pick(e, ("id", "key", "kind", "traveler", "world", "difficulty", "copies_required"))
                             for e in game_data["locations"]), key=lambda e: e["id"]),
        "travelers": sorted((_pick(t, ("key", "starts_unlocked", "skills", "progressive_item", "mastery_item"))
                             for t in game_data["travelers"]), key=lambda t: t["key"]),
        "difficulties": [_pick(d, ("key", "game_id", "rank", "has_locations")) for d in game_data["difficulties"]],
    }
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()
