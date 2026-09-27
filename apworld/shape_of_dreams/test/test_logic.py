from . import SoDTestBase
from ..locations import location_name_groups


class TestWorldClearLogic(SoDTestBase):
    def test_locked_traveler_clears(self) -> None:
        clears = {1: "World 3 Clear (Deep Sleep): Aurena",
                  2: "World 3 Clear (Ominous Dream): Aurena",
                  3: "World 3 Clear (Nightmare): Aurena"}
        for location in clears.values():
            self.assertFalse(self.can_reach_location(location))
        for copies in (1, 2, 3):
            self.collect(self.get_item_by_name("Progressive Aurena"))
            for needed, location in clears.items():
                self.assertEqual(self.can_reach_location(location), copies >= needed, f"{location} at {copies}")

    def test_starting_traveler_clears(self) -> None:
        self.assertTrue(self.can_reach_location("World 5 Clear (Deep Sleep): Mist"))
        self.assertFalse(self.can_reach_location("World 5 Clear (Ominous Dream): Mist"))
        self.assertFalse(self.can_reach_location("World 5 Clear (Nightmare): Mist"))
        self.collect(self.get_item_by_name("Progressive Mist"))
        self.assertTrue(self.can_reach_location("World 5 Clear (Ominous Dream): Mist"))
        self.assertFalse(self.can_reach_location("World 5 Clear (Nightmare): Mist"))
        self.collect(self.get_item_by_name("Progressive Mist"))
        self.assertTrue(self.can_reach_location("World 5 Clear (Nightmare): Mist"))

    def test_locked_traveler_locations_need_traveler(self) -> None:
        # Every Shell location (achievements done as Shell and world clears) needs Progressive Shell; nothing else does
        # The default goal needs all 9 Travelers, so it depends on Shell too.
        self.assertAccessDependency(sorted(location_name_groups["Shell"]) + ["Goal"], [["Progressive Shell"]])

    def test_traveler_achievements(self) -> None:
        # "Deceiving Looks" must be done as Aurena; "BFR 9000" as Lacerta, who starts unlocked.
        self.assertFalse(self.can_reach_location("Achievement: Deceiving Looks"))
        self.assertTrue(self.can_reach_location("Achievement: BFR 9000"))
        self.assertTrue(self.can_reach_location("Achievement: Pure Imagination"))
        self.collect(self.get_item_by_name("Progressive Aurena"))
        self.assertTrue(self.can_reach_location("Achievement: Deceiving Looks"))

    def test_world_clears_are_priority(self) -> None:
        from BaseClasses import LocationProgressType
        for location in self.multiworld.get_locations(self.player):
            if location.name.startswith("World "):
                self.assertEqual(location.progress_type, LocationProgressType.PRIORITY, location.name)
            elif location.address is not None:
                self.assertEqual(location.progress_type, LocationProgressType.DEFAULT, location.name)
