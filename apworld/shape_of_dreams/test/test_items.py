from collections import Counter

from . import SoDTestBase
from ..items import STARDUST, mastery_item_names
from ..locations import location_table


class _PoolTest(SoDTestBase):
    packs = 7

    def _pool(self) -> Counter:
        return Counter(item.name for item in self.multiworld.itempool if item.player == self.player)

    def test_item_count_equals_location_count(self) -> None:
        self.assertEqual(len(location_table), 228)
        self.assertEqual(sum(self._pool().values()), len(location_table))

    def test_filler_mix(self) -> None:
        pool = self._pool()
        for name in mastery_item_names:
            self.assertEqual(pool[name], self.packs)
        self.assertEqual(pool[STARDUST], 135 - 9 * self.packs)

    def test_progressive_copies(self) -> None:
        pool = self._pool()
        for traveler in ("Aurena", "Bismuth", "Cetus", "Nachia", "Shell", "Vesper", "Yubar"):
            self.assertEqual(pool[f"Progressive {traveler}"], 4)
        for traveler in ("Lacerta", "Mist"):
            self.assertEqual(pool[f"Progressive {traveler}"], 3)


class TestDefaultPool(_PoolTest):
    packs = 7


class TestNoMastery(_PoolTest):
    options = {"mastery_packs_per_traveler": 0}
    packs = 0


class TestAllMastery(_PoolTest):
    options = {"mastery_packs_per_traveler": 15}
    packs = 15
