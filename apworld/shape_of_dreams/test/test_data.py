import unittest

from . import SoDTestBase
from ..data import GAME_DATA
from ..data.data_hash import compute


class TestGameData(unittest.TestCase):
    def test_data_hash_is_current(self) -> None:
        # The mod compares this hash with its own; a stale one would reject (or wrongly accept) seeds.
        self.assertEqual(GAME_DATA["data_hash"], compute(GAME_DATA))

    def test_data_hash_ignores_display_names(self) -> None:
        renamed = {
            **GAME_DATA,
            "items": [{**e, "name": e["name"] + "!"} for e in GAME_DATA["items"]],
            "locations": [{**e, "name": e["name"] + "!"} for e in GAME_DATA["locations"]],
            "travelers": [{**t, "name": t["name"] + "!"} for t in GAME_DATA["travelers"]],
            "difficulties": [{**d, "name": d["name"] + "!"} for d in GAME_DATA["difficulties"]],
        }
        self.assertEqual(compute(renamed), GAME_DATA["data_hash"])

    def test_data_hash_sees_logic_changes(self) -> None:
        changed = {**GAME_DATA, "locations": [dict(e) for e in GAME_DATA["locations"]]}
        changed["locations"][-1]["copies_required"] = 99
        self.assertNotEqual(compute(changed), GAME_DATA["data_hash"])

    def test_id_history_covers_live_ids(self) -> None:
        for section in ("items", "locations"):
            history = GAME_DATA["id_history"][section]
            self.assertEqual(len(set(history.values())), len(history), f"{section}: an ID is used twice")
            for e in GAME_DATA[section]:
                self.assertEqual(history.get(e["key"]), e["id"], f"{section}: {e['key']}")



class TestSlotData(SoDTestBase):
    def test_slot_data_carries_data_version_and_hash(self) -> None:
        slot_data = self.world.fill_slot_data()
        self.assertEqual(slot_data["data_format_version"], GAME_DATA["data_format_version"])
        self.assertEqual(slot_data["data_hash"], GAME_DATA["data_hash"])
