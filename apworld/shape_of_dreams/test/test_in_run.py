from collections import Counter
import unittest

from BaseClasses import ItemClassification, LocationProgressType
from Fill import distribute_items_restrictive
from test.general import setup_multiworld
from worlds.AutoWorld import call_all

from .. import ShapeOfDreamsWorld
from ..items import STARDUST, curse_item_weights, in_run_item_weights, item_table, mastery_item_names
from ..locations import location_table
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
    def test_useful_mastery_avoids_exclusions_at_maximum_pack_count(self):
        # The minimum-size pool with maximum mastery has the least spare filler.
        # Fill must still succeed for every toggle combination, including the extra
        # Starless Path exclusion when passive mastery is disabled.
        for passive in (False, True):
            for in_run, traps in ((False, False), (True, False), (False, True), (True, True)):
                with self.subTest(passive=passive, in_run=in_run, traps=traps):
                    world = setup_multiworld(ShapeOfDreamsWorld, seed=7, options={
                        "jonas_wares": 0, "mastery_packs_per_traveler": 15,
                        "mastery_pack_value": 40, "passive_mastery": passive,
                        "in_run_items": in_run, "traps": traps}).worlds[1]
                    mastery = [item for item in world.multiworld.itempool if item.name in mastery_item_names]
                    self.assertEqual(len(mastery), 135)
                    self.assertTrue(all(item.classification == ItemClassification.useful for item in mastery))
                    distribute_items_restrictive(world.multiworld)
                    for location in world.get_locations():
                        if location.item.name in mastery_item_names:
                            self.assertNotEqual(location.progress_type, LocationProgressType.EXCLUDED)
                        if location.progress_type == LocationProgressType.EXCLUDED:
                            self.assertTrue(location.item.excludable)

    @staticmethod
    def finish_generation(multiworld):
        distribute_items_restrictive(multiworld)
        call_all(multiworld, "pre_output")

    def test_starting_inventory_replacement_uses_final_stardust_count(self):
        for starting in ("Mastery: Mist", STARDUST):
            with self.subTest(starting=starting):
                world = setup_multiworld(ShapeOfDreamsWorld, seed=9).worlds[1]
                multiworld = world.multiworld
                # Main.py precollects start_inventory_from_pool, then removes its pool copies after
                # generate_basic and creates replacement filler. Starting Stardust also gets paid.
                multiworld.push_precollected(world.create_item(starting))
                multiworld.itempool.remove(next(item for item in multiworld.itempool if item.name == starting))
                multiworld.itempool.append(world.create_filler())
                self.finish_generation(multiworld)
                self.assertEqual(world.fill_slot_data()["stardust_item_count"], 138)
                tracker = _tracker_world(world.fill_slot_data())
                tracker.pre_output()
                self.assertEqual(tracker.fill_slot_data(), world.fill_slot_data())

    def test_additive_starting_stardust_is_in_seed_total(self):
        world = setup_multiworld(ShapeOfDreamsWorld, seed=9).worlds[1]
        for _ in range(2):
            world.multiworld.push_precollected(world.create_item(STARDUST))
        self.finish_generation(world.multiworld)
        self.assertEqual(world.fill_slot_data()["stardust_item_count"], 139)

    def test_itemlinks_count_shared_deliveries_without_virtual_copies(self):
        for link_replacement in (False, True):
            with self.subTest(link_replacement=link_replacement):
                options = {"item_links": [{"name": "Shared Stardust", "item_pool": [STARDUST],
                                          "replacement_item": STARDUST, "link_replacement": link_replacement}]}
                multiworld = setup_multiworld([ShapeOfDreamsWorld] * 2, seed=9, options=options)
                multiworld.set_item_links()
                multiworld.link_items()
                group_id = next(iter(multiworld.groups))
                # The server delivers group-owned copies to both players, and personal replacements to
                # their owner. Virtual ItemLink locations hold the original 274 copies for logic only.
                expected = {player: sum(item.name == STARDUST and item.player in (player, group_id)
                                        for item in multiworld.itempool) for player in multiworld.player_ids}
                self.assertEqual(sum(location.address is None and location.item.name == STARDUST
                                     for location in multiworld.get_filled_locations()), 274)
                self.finish_generation(multiworld)
                for player in multiworld.player_ids:
                    self.assertEqual(multiworld.worlds[player].fill_slot_data()["stardust_item_count"], expected[player])

    def test_pool_sizes_and_stardust_for_all_options(self):
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
                        for name, location in location_table.items():
                            if location.kind == "artifact":
                                self.assertEqual(world.get_location(name).progress_type, LocationProgressType.EXCLUDED)

    def test_tracker_rebuilds_expanded_pool(self):
        world = setup_multiworld(ShapeOfDreamsWorld, seed=7,
                                 options={"jonas_wares": 3, "in_run_items": True, "traps": True}).worlds[1]
        tracker = _tracker_world(world.fill_slot_data())
        self.assertEqual(tracker.fill_slot_data(), world.fill_slot_data())
        # UT receives the actual starting Travelers from the server; its random precollect choices differ.
        self.assertEqual(Counter(i.name for i in tracker.multiworld.itempool if not i.advancement),
                         Counter(i.name for i in world.multiworld.itempool if not i.advancement))
