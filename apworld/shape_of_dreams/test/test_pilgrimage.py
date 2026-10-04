from BaseClasses import CollectionState, ItemClassification, LocationProgressType
from Fill import distribute_items_restrictive

from . import SoDTestBase
from ..locations import location_name_groups, location_table


EXPECTED = {
    "shrine": {"Pot of Greed", "Maw of Doom", "Hatred", "Paradox", "Mirror of Remorse", "Destiny",
               "Entanglement", "Altar of Cleansing", "Ascension"},
    "quest": {"Stray Memory", "Star Seeker's Journal", "Fragment of Radiance", "Call of the Ravenous",
              "Consort of Night", "Hunted by Obliviax"},
    "artifact": {"Bouquet of Eyes", "Emblem of Subjugation", "First Merchant's Token", "Fool's Gold",
                 "Forest Hound Seed", "Nightmare Catalyst", "Star Blossom", "The Starlit Stone",
                 "Tome of the Seeker", "Void Whisperer", "Watcher's Note", "Wedding Ring"},
}


class TestPilgrimage(SoDTestBase):
    def test_catalog_and_groups(self):
        for kind, names in EXPECTED.items():
            actual = {name for name, data in location_table.items() if data.kind == kind}
            self.assertEqual(actual, {f"{kind.title()}: {name}" for name in names})
            self.assertEqual(location_name_groups[kind.title() + "s"], actual)
            for name in actual:
                self.assertTrue(location_table[name].key.startswith(kind.title() + "_"))
                for group, members in location_name_groups.items():
                    if group != kind.title() + "s":
                        self.assertNotIn(name, members, group)
        self.assertNotIn("Shrine: Disintegration", location_table)
        self.assertNotIn("Quest: Guiding Compass", location_table)
        self.assertNotIn("Quest: Secret Meeting", location_table)
        self.assertNotIn("Quest: Treasure Map", location_table)

    def test_progress_types_and_no_item_requirements(self):
        empty = CollectionState(self.multiworld)
        empty.prog_items[self.player].clear()
        for name, data in location_table.items():
            if data.kind not in EXPECTED:
                continue
            location = self.world.get_location(name)
            expected = LocationProgressType.EXCLUDED if data.kind == "artifact" else LocationProgressType.DEFAULT
            self.assertEqual(location.progress_type, expected, name)
            self.assertTrue(location.can_reach(empty), name)

    def test_artifact_fill(self):
        for seed in range(5):
            self.world_setup(seed)
            distribute_items_restrictive(self.multiworld)
            for name in location_name_groups["Artifacts"]:
                self.assertEqual(self.world.get_location(name).item.classification, ItemClassification.filler)


class TestPilgrimageWithoutInRunItemsOrWares(TestPilgrimage):
    options = {"in_run_items": False, "traps": False, "jonas_wares": 0}


class TestArtifactExclusionOverridesPriority(SoDTestBase):
    options = {"priority_locations": ["Artifacts"], "mastery_packs_per_traveler": 15, "passive_mastery": False}

    def test_artifacts_stay_excluded(self):
        for name in location_name_groups["Artifacts"]:
            self.assertEqual(self.world.get_location(name).progress_type, LocationProgressType.EXCLUDED)
        distribute_items_restrictive(self.multiworld)
        for name in location_name_groups["Artifacts"]:
            self.assertEqual(self.world.get_location(name).item.classification, ItemClassification.filler)
