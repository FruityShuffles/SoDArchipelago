from typing import Any, Dict, List, Set

from BaseClasses import CollectionState, ItemClassification, LocationProgressType, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .data import GAME_DATA
from .items import (GAME_NAME, STARDUST, SoDItem, item_name_groups, item_name_to_id, item_table,
                    lucid_dreams_by_type, mastery_item_names, travelers, unlock_item_counts)
from .locations import SoDLocation, location_name_groups, location_name_to_id, location_table
from .options import SoDOptions, option_groups
from .stars import shuffle_star_requirements

# Completes on a Starless Path win, which needs a Traveler at mastery 40 (DESIGN.md "Passive mastery").
STARLESS_PATH_ACHIEVEMENT = "ACH_THE_ROAD_NOT_TAKEN"
# Achievements a game bug makes impossible: always excluded until the game fixes them (issue #6).
BROKEN_ACHIEVEMENTS = {"ACH_WHOS_THE_PREY_NOW"}


class SoDWeb(WebWorld):
    theme = "dirt"
    bug_report_page = "https://github.com/FruityShuffles/SoDArchipelago/issues"
    option_groups = option_groups
    tutorials = [Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up Shape of Dreams for Archipelago.",
        "English",
        "setup_en.md",
        "setup/en",
        ["Chainfire"],
    )]


class ShapeOfDreamsWorld(World):
    """
    Shape of Dreams is a roguelite action game where Travelers journey through dreams,
    collecting Memories and Essences to build their power.
    """
    game = GAME_NAME
    web = SoDWeb()
    options_dataclass = SoDOptions
    options: SoDOptions
    origin_region_name = "Menu"
    # Item names of the forced Lucid Dreams (DESIGN.md "Forced Lucid Dreams"), set in generate_early.
    forced_lucid_dreams: Set[str] = frozenset()
    # Star key -> shuffled mastery requirement (DESIGN.md "Shuffled star requirements"); empty when the option is off.
    star_requirements: Dict[str, int] = {}
    # The 2 Travelers whose first progressive copy is precollected (DESIGN.md "Items"), set in generate_early.
    starting_travelers: List[str] = []

    item_name_to_id = item_name_to_id
    location_name_to_id = location_name_to_id
    item_name_groups = item_name_groups
    location_name_groups = location_name_groups

    def generate_early(self) -> None:
        evil, chaotic = self.options.forced_evil_lucid_dreams.value, self.options.forced_chaotic_lucid_dreams.value
        self.forced_lucid_dreams = ({lucid_dreams_by_type["evil"][d] for d in evil} |
                                    {lucid_dreams_by_type["chaotic"][d] for d in chaotic})
        if self.options.shuffle_star_requirements:
            self.star_requirements = shuffle_star_requirements(self.random)
        self.starting_travelers = self.random.sample(sorted(travelers), 2)

    def create_item(self, name: str) -> SoDItem:
        data = item_table[name]
        # A forced dream's release is key to winning, so it's progression: fill puts it in the priority world clears.
        classification = ItemClassification.progression if name in self.forced_lucid_dreams else data.classification
        return SoDItem(name, classification, data.id, self.player)

    def get_filler_item_name(self) -> str:
        return STARDUST

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu)
        for name, data in location_table.items():
            location = SoDLocation(self.player, name, data.id, menu)
            if data.kind == "world_clear":
                location.progress_type = LocationProgressType.PRIORITY
            elif data.key in BROKEN_ACHIEVEMENTS:
                location.progress_type = LocationProgressType.EXCLUDED
            elif data.key == STARLESS_PATH_ACHIEVEMENT and not self.options.passive_mastery:
                # Without passive mastery only Mastery items reach 40, so this check only ever holds filler.
                location.progress_type = LocationProgressType.EXCLUDED
            menu.locations.append(location)

        goal = SoDLocation(self.player, "Goal", None, menu)
        goal.place_locked_item(SoDItem("Victory", ItemClassification.progression, None, self.player))
        menu.locations.append(goal)

    def create_items(self) -> None:
        pool: List[SoDItem] = []
        for name, copies in unlock_item_counts.items():
            pool += [self.create_item(name) for _ in range(copies)]
        # Each starting Traveler's first copy is a starting item instead of a pool item.
        for traveler in self.starting_travelers:
            item = next(i for i in pool if i.name == travelers[traveler]["progressive_item"])
            pool.remove(item)
            self.multiworld.push_precollected(item)
        for name in mastery_item_names:
            pool += [self.create_item(name) for _ in range(self.options.mastery_packs_per_traveler.value)]
        remaining = len(location_table) - len(pool)
        if remaining < 0:
            raise RuntimeError(f"{self.player_name}: {len(pool)} items don't fit in {len(location_table)} locations")
        pool += [self.create_item(STARDUST) for _ in range(remaining)]
        self.multiworld.itempool += pool

    def _unlocked_travelers(self, state: CollectionState) -> int:
        return sum(1 for t in travelers.values() if state.has(t["progressive_item"], self.player))

    def set_rules(self) -> None:
        for name, data in location_table.items():
            if data.traveler is None:
                continue
            # An achievement that must be done as a Traveler needs that Traveler (DESIGN.md "Logic").
            copies = data.copies_required if data.kind == "world_clear" else 1
            item = travelers[data.traveler]["progressive_item"]
            self.get_location(name).access_rule =                 lambda state, item=item, copies=copies: state.has(item, self.player, copies)

        required = self.options.goal_traveler_count.value
        self.get_location("Goal").access_rule = lambda state: self._unlocked_travelers(state) >= required
        self.multiworld.completion_condition[self.player] = lambda state: state.has("Victory", self.player)

    def fill_slot_data(self) -> Dict[str, Any]:
        return {
            "data_format_version": GAME_DATA["data_format_version"],
            "data_hash": GAME_DATA["data_hash"],
            "extracted_from_game_version": GAME_DATA["extracted_from_game_version"],
            "world_version": self.world_version.as_simple_string(),
            "goal_difficulty": self.options.goal_difficulty.current_key,
            "goal_difficulty_rank": self.options.goal_difficulty.value,
            "goal_traveler_count": self.options.goal_traveler_count.value,
            "mastery_packs_per_traveler": self.options.mastery_packs_per_traveler.value,
            "mastery_pack_value": self.options.mastery_pack_value.value,
            "stardust_pack_value": self.options.stardust_pack_value.value,
            "passive_mastery": bool(self.options.passive_mastery.value),
            "death_link": bool(self.options.death_link.value),
            "forced_lucid_dreams": sorted(item_table[name].key for name in self.forced_lucid_dreams),
            "star_requirements": dict(sorted(self.star_requirements.items())),
        }
