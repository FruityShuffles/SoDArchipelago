from BaseClasses import LocationProgressType

from . import SoDTestBase
from .. import STARLESS_PATH_ACHIEVEMENT
from ..locations import location_table

STARLESS_PATH_LOCATION = next(n for n, d in location_table.items() if d.key == STARLESS_PATH_ACHIEVEMENT)


class TestPassiveMastery(SoDTestBase):
    def test_on_by_default(self) -> None:
        self.assertTrue(self.world.fill_slot_data()["passive_mastery"])
        self.assertEqual(self.world.get_location(STARLESS_PATH_LOCATION).progress_type,
                         LocationProgressType.DEFAULT)


class TestNoPassiveMastery(SoDTestBase):
    options = {"passive_mastery": False}

    def test_starless_path_excluded(self) -> None:
        self.assertFalse(self.world.fill_slot_data()["passive_mastery"])
        self.assertEqual(self.world.get_location(STARLESS_PATH_LOCATION).progress_type,
                         LocationProgressType.EXCLUDED)


class TestMaxMasteryPackValue(SoDTestBase):
    options = {"mastery_pack_value": 40}

    def test_slot_data(self) -> None:
        self.assertEqual(self.world.fill_slot_data()["mastery_pack_value"], 40)
