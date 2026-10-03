from . import SoDTestBase
from ..items import travelers


class _GoalTest(SoDTestBase):
    count = 9

    def test_goal_needs_exactly_count_travelers(self) -> None:
        # The 2 starting Travelers count from the start; each other Traveler needs one copy. Memories never matter.
        others = [t for t in sorted(travelers) if t not in self.world.starting_travelers]
        for i, traveler in enumerate(others):
            self.assertEqual(self.can_reach_location("Goal"), 2 + i >= self.count, f"{i} other Travelers")
            self.collect(self.get_item_by_name(travelers[traveler]["progressive_item"]))
        self.assertTrue(self.can_reach_location("Goal"))

    def test_extra_copies_dont_count_twice(self) -> None:
        other = next(t for t in sorted(travelers) if t not in self.world.starting_travelers)
        self.collect(self.get_items_by_name(travelers[other]["progressive_item"]))
        self.assertEqual(self.can_reach_location("Goal"), self.count <= 3)


class TestGoalDefault(_GoalTest):
    count = 9


class TestGoalOne(_GoalTest):
    options = {"goal_traveler_count": 1}
    count = 1


class TestGoalFive(_GoalTest):
    options = {"goal_traveler_count": 5}
    count = 5
