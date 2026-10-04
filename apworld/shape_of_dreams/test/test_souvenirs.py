from BaseClasses import ItemClassification, LocationProgressType

from . import SoDTestBase
from ..locations import location_name_groups, location_table

SOUVENIRS = {n for n, d in location_table.items() if d.kind == "souvenir"}


class TestSouvenirs(SoDTestBase):
    def test_locations(self) -> None:
        self.assertEqual(len(SOUVENIRS), 17)
        self.assertEqual(location_name_groups["Souvenirs"], SOUVENIRS)
        self.assertIn("Souvenir: Mini Aurena", SOUVENIRS)
        self.assertTrue(all(location_table[n].key.startswith("Acc_General_") for n in SOUVENIRS))
        # Only the Souvenirs group: not in a Traveler's group, even the plushies.
        for group, members in location_name_groups.items():
            if group != "Souvenirs":
                self.assertFalse(members & SOUVENIRS, group)

    def test_excluded_and_free(self) -> None:
        for name in SOUVENIRS:
            self.assertEqual(self.world.get_location(name).progress_type, LocationProgressType.EXCLUDED, name)
            self.assertTrue(self.can_reach_location(name), name)


class TestSouvenirFill(SoDTestBase):
    """After fill, souvenirs only ever hold filler (default options)."""

    def test_only_filler(self) -> None:
        from Fill import distribute_items_restrictive
        for seed in range(5):
            self.world_setup(seed)
            distribute_items_restrictive(self.multiworld)
            for name in SOUVENIRS:
                item = self.world.get_location(name).item
                self.assertIsNotNone(item, name)
                self.assertEqual(item.classification, ItemClassification.filler, f"seed {seed}: {name} = {item}")


class TestSouvenirFillFewestStardust(TestSouvenirFill):
    """The most excluded locations (19) and the fewest Stardust items (17)."""
    options = {"mastery_packs_per_traveler": 15, "passive_mastery": False}
