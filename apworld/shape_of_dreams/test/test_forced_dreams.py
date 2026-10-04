from collections import Counter

from BaseClasses import ItemClassification

from . import SoDTestBase
from ..items import lucid_dreams_by_type
from ..locations import location_table
from ..options import ForcedChaoticLucidDreams, ForcedEvilLucidDreams


def _classes(test: SoDTestBase) -> dict:
    return {item.name: item.classification for item in test.multiworld.itempool
            if item.player == test.player and item.name.startswith("Lucid Dream: ")}


class TestForcedDreamOptions(SoDTestBase):
    def test_options_only_accept_their_own_type(self) -> None:
        # Types read from the prefabs (DESIGN.md "Forced Lucid Dreams"): 6 Evil, 5 Chaotic, 4 Good.
        self.assertEqual(set(ForcedEvilLucidDreams.valid_keys), set(lucid_dreams_by_type["evil"]))
        self.assertEqual(set(ForcedChaoticLucidDreams.valid_keys), set(lucid_dreams_by_type["chaotic"]))
        self.assertEqual({t: len(d) for t, d in lucid_dreams_by_type.items()}, {"good": 4, "evil": 6, "chaotic": 5})
        self.assertIn("Grievous Wounds", ForcedEvilLucidDreams.valid_keys)
        self.assertIn("WILD", ForcedChaoticLucidDreams.valid_keys)

    def test_default_forces_nothing(self) -> None:
        self.assertTrue(all(c == ItemClassification.useful for c in _classes(self).values()))
        self.assertEqual(self.world.fill_slot_data()["forced_lucid_dreams"], [])


class TestForcedDreams(SoDTestBase):
    options = {"forced_evil_lucid_dreams": ["Grievous Wounds", "Mad Life"],
               "forced_chaotic_lucid_dreams": ["WILD"]}
    forced = {"Lucid Dream: Grievous Wounds", "Lucid Dream: Mad Life", "Lucid Dream: WILD"}

    def test_forced_releases_are_progression(self) -> None:
        classes = _classes(self)
        self.assertEqual(len(classes), 15)
        for name, classification in classes.items():
            expected = ItemClassification.progression if name in self.forced else ItemClassification.useful
            self.assertEqual(classification, expected, name)

    def test_pool_size_unchanged(self) -> None:
        pool = Counter(item.name for item in self.multiworld.itempool if item.player == self.player)
        self.assertEqual(sum(pool.values()), len(self.world.enabled_locations))
        for name in self.forced:
            self.assertEqual(pool[name], 1)

    def test_slot_data_carries_keys(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["forced_lucid_dreams"],
                         ["LucidDream_GrievousWounds", "LucidDream_MadLife", "LucidDream_WILD"])


class TestAllDreamsForced(SoDTestBase):
    # Every Evil and Chaotic dream forced: 34 + 11 progression items exactly fill the 45 priority Deep Sleep clears.
    options = {"forced_evil_lucid_dreams": sorted(lucid_dreams_by_type["evil"]),
               "forced_chaotic_lucid_dreams": sorted(lucid_dreams_by_type["chaotic"])}

    def test_every_forced_release_is_progression(self) -> None:
        progression = [n for n, c in _classes(self).items() if c == ItemClassification.progression]
        self.assertEqual(len(progression), 11)
