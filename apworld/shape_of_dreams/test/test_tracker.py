from argparse import Namespace
from typing import Any, Dict

from BaseClasses import CollectionState, MultiWorld
from test.general import gen_steps
from worlds.AutoWorld import AutoWorldRegister, call_all

from . import SoDTestBase
from ..items import GAME_NAME, travelers
from ..locations import location_name_to_id, location_table


def _tracker_world(slot_data: Dict[str, Any]):
    """Generates the way Universal Tracker does without a YAML: default options plus the seed's slot_data."""
    multiworld = MultiWorld(1)
    multiworld.game[1] = GAME_NAME
    multiworld.player_name = {1: "Tracker"}
    multiworld.set_seed(None)
    multiworld.generation_is_fake = True
    multiworld.re_gen_passthrough = {GAME_NAME: slot_data}
    args = Namespace(**{name: {1: option.from_any(option.default)}
                        for name, option in AutoWorldRegister.world_types[GAME_NAME].options_dataclass.type_hints.items()})
    multiworld.set_options(args)
    multiworld.state = CollectionState(multiworld)
    for step in gen_steps:
        call_all(multiworld, step)
    return multiworld.worlds[1]


class TestTrackerRegen(SoDTestBase):
    options = {"goal_difficulty": "ominous_dream", "goal_traveler_count": 3, "mastery_packs_per_traveler": 2,
               "passive_mastery": False, "death_link": True, "forced_evil_lucid_dreams": ["Mad Life"],
               "forced_chaotic_lucid_dreams": ["WILD"], "stardust_total": 12345, "in_run_items": True,
               "traps": True, "jonas_wares": 7}

    def test_regen_matches_the_seed(self) -> None:
        slot_data = self.world.fill_slot_data()
        tracker = _tracker_world(self.world.interpret_slot_data(slot_data))
        self.assertEqual(tracker.fill_slot_data(), slot_data)
        self.assertEqual(tracker.forced_lucid_dreams, self.world.forced_lucid_dreams)
        for location in self.world.get_locations():
            self.assertEqual(tracker.get_location(location.name).progress_type, location.progress_type, location.name)

    def test_regen_uses_the_seed_goal_traveler_count(self) -> None:
        tracker = _tracker_world(self.world.fill_slot_data())
        # Universal Tracker drops precollected items: the server sends the starting Travelers.
        state = CollectionState(tracker.multiworld)
        state.prog_items[tracker.player].clear()
        for traveler in sorted(travelers)[:3]:
            self.assertFalse(tracker.get_location("Goal").can_reach(state))
            state.collect(tracker.create_item(travelers[traveler]["progressive_item"]), True)
        self.assertTrue(tracker.get_location("Goal").can_reach(state))

    def test_achievements_have_descriptions(self) -> None:
        aliases = self.world.location_id_to_alias
        achievement_ids = {data.id for data in location_table.values() if data.kind == "achievement"}
        self.assertEqual(set(aliases), achievement_ids)
        self.assertTrue(all(alias and not alias.endswith(".") for alias in aliases.values()))
        self.assertEqual(aliases[location_name_to_id["Achievement: Déjà vu"]], "Pet the cat")

    def test_older_seed_without_newer_keys(self) -> None:
        slot_data = self.world.fill_slot_data()
        for key in ("passive_mastery", "forced_lucid_dreams", "star_requirements"):
            del slot_data[key]
        tracker = _tracker_world(slot_data)
        self.assertTrue(tracker.options.passive_mastery)
        self.assertEqual(tracker.forced_lucid_dreams, set())
        self.assertEqual(tracker.star_requirements, {})
