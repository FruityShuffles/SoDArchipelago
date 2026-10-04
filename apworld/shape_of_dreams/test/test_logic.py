from . import SoDTestBase
from ..items import travelers
from ..locations import location_name_groups, location_table


def _achievement(traveler: str) -> str:
    return next(n for n, d in sorted(location_table.items()) if d.kind == "achievement" and d.traveler == traveler)


class _LogicTest(SoDTestBase):
    @property
    def starter(self) -> str:
        return self.world.starting_travelers[0]

    @property
    def other(self) -> str:
        return next(t for t in sorted(travelers) if t not in self.world.starting_travelers)

    def _clears(self, traveler: str) -> dict:
        name = travelers[traveler]["name"]
        return {1: f"World 3 Clear (Deep Sleep): {name}",
                2: f"World 3 Clear (Ominous Dream): {name}",
                3: f"World 3 Clear (Nightmare): {name}"}


class TestWorldClearLogic(_LogicTest):
    def test_other_traveler_clears(self) -> None:
        clears = self._clears(self.other)
        for location in clears.values():
            self.assertFalse(self.can_reach_location(location))
        for copies in (1, 2, 3):
            self.collect(self.get_item_by_name(travelers[self.other]["progressive_item"]))
            for needed, location in clears.items():
                self.assertEqual(self.can_reach_location(location), copies >= needed, f"{location} at {copies}")

    def test_starting_traveler_clears(self) -> None:
        # The precollected copy counts: Deep Sleep is reachable with no items, then the same rules as everyone else.
        clears = self._clears(self.starter)
        for copies in (1, 2, 3):
            for needed, location in clears.items():
                self.assertEqual(self.can_reach_location(location), copies >= needed, f"{location} at {copies}")
            if copies < 3:
                self.collect(self.get_item_by_name(travelers[self.starter]["progressive_item"]))

    def test_other_traveler_locations_need_traveler(self) -> None:
        # Every location of a non-starting Traveler (achievements done as them and world clears) needs their progressive
        # item. The default goal needs all 9 Travelers, so it depends on them too.
        t = travelers[self.other]
        self.assertAccessDependency(sorted(location_name_groups[t["name"]]) + ["Goal"], [[t["progressive_item"]]])

    def test_traveler_achievements(self) -> None:
        self.assertTrue(self.can_reach_location(_achievement(self.starter)))
        self.assertFalse(self.can_reach_location(_achievement(self.other)))
        self.assertTrue(self.can_reach_location("Achievement: Pure Imagination"))
        self.collect(self.get_item_by_name(travelers[self.other]["progressive_item"]))
        self.assertTrue(self.can_reach_location(_achievement(self.other)))

    def test_nightmare_dream_achievement(self) -> None:
        # Entering the Pure White Dream on Nightmare is a World 4 Nightmare clear with any Traveler.
        for copies in (1, 2, 3):
            self.assertEqual(self.can_reach_location("Achievement: Vivid Dream"), copies >= 3, f"{copies} copies")
            if copies < 3:
                self.collect(self.get_item_by_name(travelers[self.other]["progressive_item"]))
                self.assertFalse(self.can_reach_location("Achievement: Vivid Dream"))
                self.collect(self.get_item_by_name(travelers[self.starter]["progressive_item"]))

    def test_progress_types(self) -> None:
        from BaseClasses import LocationProgressType
        from .. import BROKEN_ACHIEVEMENTS
        broken = {n for n, d in location_table.items() if d.key in BROKEN_ACHIEVEMENTS}
        self.assertEqual(len(broken), len(BROKEN_ACHIEVEMENTS))
        for location in self.multiworld.get_locations(self.player):
            if location.name.startswith("World ") and "(Deep Sleep)" in location.name:
                self.assertEqual(location.progress_type, LocationProgressType.PRIORITY, location.name)
            elif location.name in broken or location.name.startswith("Souvenir: "):
                self.assertEqual(location.progress_type, LocationProgressType.EXCLUDED, location.name)
            elif location.address is not None:
                self.assertEqual(location.progress_type, LocationProgressType.DEFAULT, location.name)


class TestWorldClearLogicLacertaStarts(TestWorldClearLogic):
    """A fixed seed where Lacerta, a vanilla starter, is also a starting Traveler."""

    def setUp(self) -> None:
        self.world_setup(6)

    @property
    def starter(self) -> str:
        self.assertIn("Hero_Lacerta", self.world.starting_travelers)
        return "Hero_Lacerta"
