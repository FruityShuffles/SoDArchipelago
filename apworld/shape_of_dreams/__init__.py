from typing import Any, Dict, List, Set

from BaseClasses import CollectionState, ItemClassification, LocationProgressType, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .data import GAME_DATA
from .items import (GAME_NAME, STARDUST, SoDItem, item_name_groups, item_name_to_id, item_table,
                    lucid_dreams_by_type, mastery_item_names, travelers, unlock_item_counts)
from .locations import (SoDLocation, location_id_to_alias, location_name_groups, location_name_to_id,
                        location_table)
from .options import SoDOptions, option_groups
from .pool import new_slot_counts
from .stars import shuffle_star_requirements

# Completes on a Starless Path win, which needs a Traveler at mastery 40 (DESIGN.md "Logic", "Passive mastery").
STARLESS_PATH_ACHIEVEMENT = "ACH_THE_ROAD_NOT_TAKEN"
# "Enter the Pure White Dream on Nightmare difficulty": a World 4 Nightmare clear with any Traveler (DESIGN.md "Logic").
NIGHTMARE_DREAM_ACHIEVEMENT = "ACH_VIVID_DREAM"
NIGHTMARE_COPIES = max(d.copies_required for d in location_table.values())
# Achievements a game bug makes impossible: always excluded until the game fixes them (issue #6).
BROKEN_ACHIEVEMENTS = {"ACH_WHOS_THE_PREY_NOW"}
# Only these clears are priority: every priority location takes some player's progression, so more would pull a
# multiworld's progression into SoD runs (DESIGN.md "Locations").
PRIORITY_DIFFICULTY = "DEEP_SLEEP"
BASE_LOCATION_KINDS = {"achievement", "world_clear", "souvenir"}


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

    # Universal Tracker can rebuild the world from the seed's slot_data alone, without the player's YAML.
    ut_can_gen_without_yaml = True
    location_id_to_alias = location_id_to_alias

    def generate_early(self) -> None:
        # Universal Tracker passes back what interpret_slot_data returned: the seed's real settings.
        slot_data = getattr(self.multiworld, "re_gen_passthrough", {}).get(GAME_NAME)
        self._stardust_count_from_slot_data = slot_data is not None
        if slot_data is not None:
            self._apply_slot_data(slot_data)
        else:
            evil, chaotic = self.options.forced_evil_lucid_dreams.value, self.options.forced_chaotic_lucid_dreams.value
            self.forced_lucid_dreams = ({lucid_dreams_by_type["evil"][d] for d in evil} |
                                        {lucid_dreams_by_type["chaotic"][d] for d in chaotic})
            if self.options.shuffle_star_requirements:
                self.star_requirements = shuffle_star_requirements(self.random)
        # Universal Tracker drops precollected items and uses the starting items the server sends instead.
        self.starting_travelers = self.random.sample(sorted(travelers), 2)
        # Build once and use the same enabled checks for regions, rules and pool sizing.
        self.enabled_locations = {name: data for name, data in location_table.items()
                                  if data.kind != "ware" or data.number <= self.options.jonas_wares.value}

    def _apply_slot_data(self, slot_data: Dict[str, Any]) -> None:
        options = self.options
        options.goal_difficulty.value = slot_data["goal_difficulty_rank"]
        options.goal_traveler_count.value = slot_data["goal_traveler_count"]
        options.mastery_packs_per_traveler.value = slot_data["mastery_packs_per_traveler"]
        options.mastery_pack_value.value = slot_data["mastery_pack_value"]
        options.stardust_total.value = slot_data["stardust_total"]
        self.stardust_item_count = slot_data["stardust_item_count"]
        options.in_run_items.value = int(slot_data["in_run_items"])
        options.traps.value = int(slot_data["traps"])
        options.jonas_wares.value = slot_data["jonas_wares"]
        # Keys added after the first release: a missing one means the seed predates its option (its vanilla default).
        options.passive_mastery.value = int(slot_data.get("passive_mastery", True))
        options.death_link.value = int(slot_data["death_link"])
        forced_keys = set(slot_data.get("forced_lucid_dreams", []))
        self.forced_lucid_dreams = {name for name, data in item_table.items() if data.key in forced_keys}
        self.star_requirements = dict(slot_data.get("star_requirements", {}))
        options.shuffle_star_requirements.value = int(bool(self.star_requirements))

    @staticmethod
    def interpret_slot_data(slot_data: Dict[str, Any]) -> Dict[str, Any]:
        """Universal Tracker: regenerate with the seed's settings (read back in generate_early). Static, so UT skips
        its first generation and only generates once connected."""
        return slot_data

    def create_item(self, name: str) -> SoDItem:
        data = item_table[name]
        # A forced dream's release is key to winning, so it's progression: fill puts it on the priority clears.
        classification = ItemClassification.progression if name in self.forced_lucid_dreams else data.classification
        return SoDItem(name, classification, data.id, self.player)

    def get_filler_item_name(self) -> str:
        return STARDUST

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        self.multiworld.regions.append(menu)
        for name, data in self.enabled_locations.items():
            location = SoDLocation(self.player, name, data.id, menu)
            if data.kind == "world_clear" and data.difficulty == PRIORITY_DIFFICULTY:
                location.progress_type = LocationProgressType.PRIORITY
            elif data.kind in ("souvenir", "artifact"):
                # Souvenirs and artifacts come from random offers/finds, so they only ever hold filler.
                location.progress_type = LocationProgressType.EXCLUDED
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
        base_locations = sum(data.kind in BASE_LOCATION_KINDS for data in self.enabled_locations.values())
        remaining = base_locations - len(pool)
        if remaining < 0:
            raise RuntimeError(f"{self.player_name}: {len(pool)} items don't fit in {base_locations} baseline locations")
        pool += [self.create_item(STARDUST) for _ in range(remaining)]
        new_slots = len(self.enabled_locations) - base_locations
        for name, count in new_slot_counts(new_slots, bool(self.options.in_run_items), bool(self.options.traps)).items():
            pool += [self.create_item(name) for _ in range(count)]
        if not self._stardust_count_from_slot_data:
            self.stardust_item_count = sum(item.name == STARDUST for item in pool)
        self.multiworld.itempool += pool

    def pre_output(self) -> None:
        if self._stardust_count_from_slot_data:
            return
        # Common options, ItemLinks and plando can change the count after create_items. Count exactly the
        # deliveries represented in multidata, excluding ItemLink's virtual event copies (address=None).
        recipients = {self.player} | {group_id for group_id, group in self.multiworld.groups.items()
                                     if self.player in group["players"]}
        self.stardust_item_count = sum(
            location.address is not None and location.item is not None and
            location.item.player in recipients and location.item.name == STARDUST
            for location in self.multiworld.get_locations())
        self.stardust_item_count += sum(item.name == STARDUST
                                       for item in self.multiworld.precollected_items[self.player])

    def _unlocked_travelers(self, state: CollectionState) -> int:
        return sum(1 for t in travelers.values() if state.has(t["progressive_item"], self.player))

    def set_rules(self) -> None:
        progressive_items = [t["progressive_item"] for t in travelers.values()]
        every_copy = {item: unlock_item_counts[item] for item in progressive_items}
        for name, data in self.enabled_locations.items():
            if data.key == NIGHTMARE_DREAM_ACHIEVEMENT:
                self.get_location(name).access_rule = lambda state: any(
                    state.has(item, self.player, NIGHTMARE_COPIES) for item in progressive_items)
            elif data.key == STARLESS_PATH_ACHIEVEMENT:
                # Mastery 40 really comes at the end of a seed: every copy is the latest stand-in logic has.
                self.get_location(name).access_rule = lambda state: state.has_all_counts(every_copy, self.player)
            if data.traveler is None:
                continue
            # An achievement that must be done as a Traveler needs that Traveler (DESIGN.md "Logic").
            copies = data.copies_required if data.kind == "world_clear" else 1
            item = travelers[data.traveler]["progressive_item"]
            self.get_location(name).access_rule = \
                lambda state, item=item, copies=copies: state.has(item, self.player, copies)

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
            "stardust_total": self.options.stardust_total.value,
            "stardust_item_count": self.stardust_item_count,
            "in_run_items": bool(self.options.in_run_items.value),
            "traps": bool(self.options.traps.value),
            "jonas_wares": self.options.jonas_wares.value,
            "passive_mastery": bool(self.options.passive_mastery.value),
            "death_link": bool(self.options.death_link.value),
            "forced_lucid_dreams": sorted(item_table[name].key for name in self.forced_lucid_dreams),
            "star_requirements": dict(sorted(self.star_requirements.items())),
        }
