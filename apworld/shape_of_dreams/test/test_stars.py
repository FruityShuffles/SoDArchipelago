from collections import Counter
from random import Random

from . import SoDTestBase
from ..data import GAME_DATA
from ..stars import shuffle_star_requirements, star_groups


class TestStarData(SoDTestBase):
    def test_groups(self) -> None:
        # 305 stars: 63 common ones in 3 categories, and every Traveler's in 4 (DESIGN.md "Shuffled star requirements").
        self.assertEqual(len(GAME_DATA["stars"]), 305)
        self.assertEqual(sum(len(g) for g in star_groups.values()), 305)
        self.assertEqual(len(star_groups), 3 + 9 * 4)
        self.assertEqual(max(s["required_level"] for s in GAME_DATA["stars"] if s["traveler"] is None), 75)
        self.assertEqual(max(s["required_level"] for s in GAME_DATA["stars"] if s["traveler"]), 35)


class TestVanillaStars(SoDTestBase):
    options = {"shuffle_star_requirements": False}

    def test_off_shuffles_nothing(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["star_requirements"], {})


class TestShuffledStars(SoDTestBase):
    def test_on_by_default(self) -> None:
        self.assertTrue(self.world.options.shuffle_star_requirements)

    def test_levels_stay_in_their_group(self) -> None:
        shuffled = self.world.fill_slot_data()["star_requirements"]
        self.assertEqual(set(shuffled), {s["key"] for s in GAME_DATA["stars"]})
        for (traveler, category), group in star_groups.items():
            self.assertEqual(Counter(shuffled[s["key"]] for s in group),
                             Counter(s["required_level"] for s in group), f"{traveler} {category}")

    def test_something_moves(self) -> None:
        shuffled = self.world.fill_slot_data()["star_requirements"]
        self.assertTrue(any(shuffled[s["key"]] != s["required_level"] for s in GAME_DATA["stars"]))

    def test_same_random_same_result(self) -> None:
        self.assertEqual(shuffle_star_requirements(Random(5)), shuffle_star_requirements(Random(5)))
