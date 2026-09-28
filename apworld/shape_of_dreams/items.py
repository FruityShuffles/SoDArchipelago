from typing import Dict, List, NamedTuple, Optional

from BaseClasses import Item, ItemClassification

from .data import GAME_DATA

GAME_NAME = "Shape of Dreams"


class SoDItem(Item):
    game = GAME_NAME


class ItemData(NamedTuple):
    id: int
    key: str
    kind: str
    classification: ItemClassification
    traveler: Optional[str]


_CLASSIFICATIONS = {
    "progression": ItemClassification.progression,
    "useful": ItemClassification.useful,
    "filler": ItemClassification.filler,
}

item_table: Dict[str, ItemData] = {
    e["name"]: ItemData(e["id"], e["key"], e["kind"], _CLASSIFICATIONS[e["classification"]], e.get("traveler"))
    for e in GAME_DATA["items"]
}

item_name_to_id: Dict[str, int] = {name: data.id for name, data in item_table.items()}

# Traveler key -> display data from game_data.json.
travelers: Dict[str, dict] = {t["key"]: t for t in GAME_DATA["travelers"]}

# Items with a fixed number of copies in every seed: one per progressive unlock step, one per other unlock.
unlock_item_counts: Dict[str, int] = {
    e["name"]: len(e["unlocks"]) for e in GAME_DATA["items"] if e["kind"] in ("progressive", "memory", "essence",
                                                                            "lucid_dream")
}

mastery_item_names: List[str] = [t["mastery_item"] for t in GAME_DATA["travelers"]]

# DESIGN.md "Forced Lucid Dreams": dream name (the item name without its prefix) -> item name, per LucidDream type.
LUCID_DREAM_PREFIX = "Lucid Dream: "
lucid_dreams_by_type: Dict[str, Dict[str, str]] = {}
for _e in GAME_DATA["items"]:
    if _e["kind"] == "lucid_dream":
        lucid_dreams_by_type.setdefault(_e["lucid_dream_type"], {})[_e["name"][len(LUCID_DREAM_PREFIX):]] = _e["name"]
STARDUST = "Stardust"


def _names(kind: str) -> set:
    return {n for n, d in item_table.items() if d.kind == kind}


item_name_groups = {
    "Progressive Travelers": _names("progressive"),
    "Memories": _names("memory"),
    "Essences": _names("essence"),
    "Lucid Dreams": _names("lucid_dream"),
    "Mastery": _names("mastery"),
}
