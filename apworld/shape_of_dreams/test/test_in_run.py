from collections import Counter
import unittest
from unittest.mock import patch

from BaseClasses import ItemClassification, LocationProgressType
from test.general import setup_multiworld

from .. import ShapeOfDreamsWorld
from ..items import STARDUST, curse_item_weights, in_run_item_weights, item_table
from ..locations import LocationData, location_table
from ..pool import largest_remainder, new_slot_counts
from .test_tracker import _tracker_world


class TestNewSlotAllocation(unittest.TestCase):
    def test_largest_remainder_and_ties(self):
        self.assertEqual(largest_remainder(7, {"a": 3, "b": 2, "c": 1}), {"a": 4, "b": 2, "c": 1})
        self.assertEqual(largest_remainder(2, {"a": 1, "b": 1, "c": 1}), {"a": 1, "b": 1, "c": 0})

    def test_all_slot_counts_and_toggle_combinations(self):
        for slots in range(128):  # #8/#9: 0–100 wares + 27 side-content checks
            for in_run in (False, True):
                for traps in (False, True):
                    with self.subTest(slots=slots, in_run=in_run, traps=traps):
                        counts = new_slot_counts(slots, in_run, traps)
                        self.assertEqual(sum(counts.values()), slots)
                        curses = sum(counts[name] for name in curse_item_weights)
                        self.assertEqual(curses, (slots + 2) // 5 if traps else 0)
                        for name, count in counts.items():
                            if count:
                                expected = ItemClassification.trap if name in curse_item_weights else ItemClassification.filler
                                self.assertEqual(item_table[name].classification, expected)
                        if not in_run:
                            self.assertEqual(counts[STARDUST], slots - curses)
                        else:
                            self.assertNotIn(STARDUST, counts)

    def test_default_mix(self):
        counts = new_slot_counts(57, True, False)
        self.assertEqual(counts["Totally Genuine Treasure Map"], 5)
        self.assertEqual(counts["Cloak of Guidance"], 2)
        self.assertEqual(counts["Clairvoyance"], 2)
        self.assertEqual(len(in_run_item_weights), 20)
        self.assertEqual(sum(counts.values()), 57)


class TestExpandedPool(unittest.TestCase):
    # Only #9's future checks are fixtures; wares exercise the real generated table.
    @staticmethod
    def _locations():
        extra = {}
        for kind, count in (("shrine", 9), ("quest", 6), ("artifact", 12)):
            for n in range(count):
                name = f"Test {kind} {n}"
                extra[name] = LocationData(9000100 + len(extra), name, kind, None, None, 0)
        return {**location_table, **extra}

    def test_pool_sizes_and_stardust_for_all_options(self):
        with patch("worlds.shape_of_dreams.location_table", self._locations()):
            for wares in (0, 30, 100):
                for packs in (0, 8, 15):
                    for in_run, traps in ((False, False), (True, False), (False, True), (True, True)):
                        with self.subTest(wares=wares, packs=packs, in_run=in_run, traps=traps):
                            options = {"jonas_wares": wares, "mastery_packs_per_traveler": packs,
                                       "in_run_items": in_run, "traps": traps, "stardust_total": 12345}
                            world = setup_multiworld(ShapeOfDreamsWorld, seed=7, options=options).worlds[1]
                            pool = Counter(item.name for item in world.multiworld.itempool)
                            new_counts = new_slot_counts(wares + 27, in_run, traps)
                            self.assertEqual(sum(pool.values()), 245 + wares + 27)
                            self.assertEqual(len(world.get_locations()), sum(pool.values()) + 1)  # Goal event
                            self.assertEqual(pool[STARDUST], 152 - 9 * packs + new_counts.get(STARDUST, 0))
                            slot_data = world.fill_slot_data()
                            self.assertEqual(slot_data["stardust_item_count"], pool[STARDUST])
                            quotient, remainder = divmod(slot_data["stardust_total"], pool[STARDUST])
                            self.assertEqual(quotient * pool[STARDUST] + remainder, 12345)
                            for name, count in new_counts.items():
                                if name != STARDUST:
                                    self.assertEqual(pool[name], count)
                            for n in range(12):
                                self.assertEqual(world.get_location(f"Test artifact {n}").progress_type,
                                                 LocationProgressType.EXCLUDED)

    def test_tracker_rebuilds_expanded_pool(self):
        with patch("worlds.shape_of_dreams.location_table", self._locations()):
            world = setup_multiworld(ShapeOfDreamsWorld, seed=7,
                                     options={"jonas_wares": 3, "in_run_items": True, "traps": True}).worlds[1]
            tracker = _tracker_world(world.fill_slot_data())
            self.assertEqual(tracker.fill_slot_data(), world.fill_slot_data())
            # UT receives the actual starting Travelers from the server; its random precollect choices differ.
            self.assertEqual(Counter(i.name for i in tracker.multiworld.itempool if not i.advancement),
                             Counter(i.name for i in world.multiworld.itempool if not i.advancement))
