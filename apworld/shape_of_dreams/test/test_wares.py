from collections import Counter
import unittest

from BaseClasses import CollectionState, LocationProgressType
from test.general import setup_multiworld

from .. import ShapeOfDreamsWorld
from ..items import STARDUST
from ..locations import location_name_groups, location_table
from ..pool import new_slot_counts
from .test_tracker import _tracker_world


class TestJonasWares(unittest.TestCase):
    def test_catalog(self):
        wares = {name: data for name, data in location_table.items() if data.kind == "ware"}
        self.assertEqual(set(wares), {f"Jonas's Ware {n}" for n in range(1, 101)})
        self.assertEqual(location_name_groups["Jonas's Wares"], set(wares))
        for n in range(1, 101):
            self.assertEqual(wares[f"Jonas's Ware {n}"].key, f"WARE_JONAS_{n}")
            self.assertEqual(wares[f"Jonas's Ware {n}"].number, n)

    def test_options_pool_logic_and_tracker(self):
        for count in (0, 1, 30, 100):
            for in_run, traps in ((False, False), (True, False), (False, True), (True, True)):
                with self.subTest(count=count, in_run=in_run, traps=traps):
                    world = setup_multiworld(ShapeOfDreamsWorld, seed=8, options={
                        "jonas_wares": count, "in_run_items": in_run, "traps": traps}).worlds[1]
                    actual = {l.name for l in world.get_locations() if l.name.startswith("Jonas's Ware ")}
                    expected = {f"Jonas's Ware {n}" for n in range(1, count + 1)}
                    self.assertEqual(actual, expected)
                    pool = Counter(i.name for i in world.multiworld.itempool)
                    self.assertEqual(sum(pool.values()), 272 + count)
                    mix = new_slot_counts(count + 27, in_run, traps)
                    self.assertEqual(pool[STARDUST], 80 + mix.get(STARDUST, 0))
                    for name, number in mix.items():
                        if name != STARDUST:
                            self.assertEqual(pool[name], number)
                    empty = CollectionState(world.multiworld)
                    empty.prog_items[1].clear()
                    for name in expected:
                        location = world.get_location(name)
                        self.assertEqual(location.progress_type, LocationProgressType.DEFAULT)
                        self.assertTrue(location.can_reach(empty))
                    tracker = _tracker_world(world.fill_slot_data())
                    self.assertEqual(set(tracker.enabled_locations), set(world.enabled_locations))
                    self.assertEqual(tracker.fill_slot_data(), world.fill_slot_data())
