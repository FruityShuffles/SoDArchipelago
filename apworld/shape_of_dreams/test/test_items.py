from collections import Counter

from . import SoDTestBase
from ..items import STARDUST, mastery_item_names, travelers
from ..locations import location_table


class _PoolTest(SoDTestBase):
    packs = 8

    def _pool(self) -> Counter:
        return Counter(item.name for item in self.multiworld.itempool if item.player == self.player)

    def test_item_count_equals_location_count(self) -> None:
        self.assertEqual(len(location_table), 245)
        self.assertEqual(sum(self._pool().values()), len(location_table))

    def test_filler_mix(self) -> None:
        pool = self._pool()
        for name in mastery_item_names:
            self.assertEqual(pool[name], self.packs)
        self.assertEqual(pool[STARDUST], 152 - 9 * self.packs)

    def test_progressive_copies(self) -> None:
        # Each starting Traveler's first copy is a starting item, not in the pool.
        pool = self._pool()
        for key, t in travelers.items():
            expected = 3 if key in self.world.starting_travelers else 4
            self.assertEqual(pool[t["progressive_item"]], expected, key)
        self.assertEqual(sum(pool[t["progressive_item"]] for t in travelers.values()), 34)


class TestDefaultPool(_PoolTest):
    packs = 8

    def test_stardust_default(self) -> None:
        # 80 x 675 = 54,000 covers every star level, souvenir and star slot (53,415; DESIGN.md "Items").
        self.assertEqual(self._pool()[STARDUST], 80)
        self.assertEqual(self.world.fill_slot_data()["stardust_pack_value"], 675)


class TestNoMastery(_PoolTest):
    options = {"mastery_packs_per_traveler": 0}
    packs = 0


class TestAllMastery(_PoolTest):
    options = {"mastery_packs_per_traveler": 15}
    packs = 15
