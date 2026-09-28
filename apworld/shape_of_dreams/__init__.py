from typing import Any, Dict, List

from BaseClasses import CollectionState, ItemClassification, LocationProgressType, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .data import GAME_DATA
from .items import (GAME_NAME, STARDUST, SoDItem, item_name_groups, item_name_to_id, item_table,
                    mastery_item_names, travelers, unlock_item_counts)
from .locations import SoDLocation, location_name_groups, location_name_to_id, location_table
from .options import SoDOptions, option_groups


class SoDWeb(WebWorld):
    theme = "dirt"
    option_groups = option_groups
    tutorials = [Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up Shape of Dreams for Archipelago.",
        "English",
        "setup_en.md",
        "setup/en",
        ["SoDArchipelago contributors"],
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

    item_name_to_id = item_name_to_id
    location_name_to_id = location_name_to_id
    item_name_groups = item_name_groups
    location_name_groups = location_name_groups

    def create_item(self, name: str) -> SoDItem:
        data = item_table[name]
        return SoDItem(name, data.classification, data.id, self.player)

    def get_filler_item_name(self) -> str:
        return STARDUST

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu)
        for name, data in location_table.items():
            location = SoDLocation(self.player, name, data.id, menu)
            if data.kind == "world_clear":
                location.progress_type = LocationProgressType.PRIORITY
            menu.locations.append(location)

        goal = SoDLocation(self.player, "Goal", None, menu)
        goal.place_locked_item(SoDItem("Victory", ItemClassification.progression, None, self.player))
        menu.locations.append(goal)

    def create_items(self) -> None:
        pool: List[SoDItem] = []
        for name, copies in unlock_item_counts.items():
            pool += [self.create_item(name) for _ in range(copies)]
        for name in mastery_item_names:
            pool += [self.create_item(name) for _ in range(self.options.mastery_packs_per_traveler.value)]
        remaining = len(location_table) - len(pool)
        if remaining < 0:
            raise RuntimeError(f"{self.player_name}: {len(pool)} items don't fit in {len(location_table)} locations")
        pool += [self.create_item(STARDUST) for _ in range(remaining)]
        self.multiworld.itempool += pool

    def _copies_rule(self, traveler: str, copies: int):
        item = travelers[traveler]["progressive_item"]
        if copies <= 0:
            return None
        return lambda state: state.has(item, self.player, copies)

    def _unlocked_travelers(self, state: CollectionState) -> int:
        return sum(1 for key, t in travelers.items()
                   if t["starts_unlocked"] or state.has(t["progressive_item"], self.player))

    def set_rules(self) -> None:
        for name, data in location_table.items():
            if data.traveler is None:
                continue
            starts_unlocked = travelers[data.traveler]["starts_unlocked"]
            if data.kind == "world_clear":
                copies = data.copies_required
            else:
                # An achievement that must be done as a Traveler needs that Traveler (DESIGN.md "Logic").
                copies = 0 if starts_unlocked else 1
            rule = self._copies_rule(data.traveler, copies)
            if rule:
                self.get_location(name).access_rule = rule

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
            "death_link": bool(self.options.death_link.value),
        }
