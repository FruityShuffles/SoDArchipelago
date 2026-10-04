from typing import Dict, NamedTuple, Optional

from BaseClasses import Location

from .data import GAME_DATA
from .items import GAME_NAME


class SoDLocation(Location):
    game = GAME_NAME


class LocationData(NamedTuple):
    id: int
    key: str
    kind: str
    traveler: Optional[str]
    # World clears only: the difficulty key (e.g. "DEEP_SLEEP").
    difficulty: Optional[str]
    # World clears only: copies of the Traveler's progressive item needed (DESIGN.md "Logic").
    copies_required: int


location_table: Dict[str, LocationData] = {
    e["name"]: LocationData(e["id"], e["key"], e["kind"], e["traveler"], e.get("difficulty"),
                            e.get("copies_required", 0))
    for e in GAME_DATA["locations"]
}

location_name_to_id: Dict[str, int] = {name: data.id for name, data in location_table.items()}

# Universal Tracker shows "<name> (<alias>)": each achievement's in-game description.
location_id_to_alias: Dict[int, str] = {e["id"]: e["description"] for e in GAME_DATA["locations"] if "description" in e}

location_name_groups = {name: set(members) for name, members in GAME_DATA["location_groups"].items()}
