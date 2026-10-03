import unittest

from test.general import setup_solo_multiworld

from . import SoDTestBase
from .. import ShapeOfDreamsWorld
from ..items import travelers


class TestStartingTravelers(SoDTestBase):
    def test_two_distinct_starters_precollected_once(self) -> None:
        starters = self.world.starting_travelers
        self.assertEqual(len(set(starters)), 2)
        precollected = [i.name for i in self.multiworld.precollected_items[self.player]]
        self.assertCountEqual(precollected, [travelers[t]["progressive_item"] for t in starters])


class TestStartingTravelerSeeds(unittest.TestCase):
    seeds = range(1, 21)

    @staticmethod
    def _starters(seed: int) -> tuple:
        multiworld = setup_solo_multiworld(ShapeOfDreamsWorld, ("generate_early",), seed)
        return tuple(multiworld.worlds[1].starting_travelers)

    def test_same_seed_same_starters(self) -> None:
        self.assertEqual(self._starters(7), self._starters(7))

    def test_seeds_give_several_pairs(self) -> None:
        pairs = {frozenset(self._starters(seed)) for seed in self.seeds}
        self.assertGreaterEqual(len(pairs), 5)
        self.assertEqual({t for pair in pairs for t in pair}, set(travelers))
