from . import SoDTestBase

LOCKED = ["Aurena", "Bismuth", "Cetus", "Nachia", "Shell", "Vesper", "Yubar"]


class _GoalTest(SoDTestBase):
    count = 9

    def test_goal_needs_exactly_count_travelers(self) -> None:
        # Lacerta and Mist count from the start; each locked Traveler needs one copy. Memories never matter.
        self.collect_by_name(["Progressive Lacerta", "Progressive Mist"])
        needed = max(0, self.count - 2)
        for i, traveler in enumerate(LOCKED):
            self.assertEqual(self.can_reach_location("Goal"), i >= needed, f"{i} locked Travelers")
            self.collect(self.get_item_by_name(f"Progressive {traveler}"))
        self.assertTrue(self.can_reach_location("Goal"))

    def test_extra_copies_dont_count_twice(self) -> None:
        if self.count <= 2:
            return
        self.collect(self.get_items_by_name("Progressive Aurena"))
        self.assertEqual(self.can_reach_location("Goal"), self.count <= 3)


class TestGoalDefault(_GoalTest):
    count = 9


class TestGoalOne(_GoalTest):
    options = {"goal_traveler_count": 1}
    count = 1


class TestGoalFive(_GoalTest):
    options = {"goal_traveler_count": 5}
    count = 5
