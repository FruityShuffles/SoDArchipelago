from dataclasses import dataclass

from Options import Choice, DeathLink, OptionGroup, PerGameCommonOptions, Range


class GoalDifficulty(Choice):
    """The lowest difficulty a winning run counts at for your goal. Winning on a harder difficulty also counts.
    A win is reaching either ending: the Pure White Dream or the Starless Path."""
    display_name = "Goal Difficulty"
    option_deep_sleep = 1
    option_ominous_dream = 2
    option_nightmare = 3
    default = option_nightmare


class GoalTravelerCount(Range):
    """How many different Travelers must win a run (at Goal Difficulty or harder) to complete your goal.
    Lacerta and Mist start unlocked; the other Travelers must be received first."""
    display_name = "Goal Traveler Count"
    range_start = 1
    range_end = 9
    default = 9


class MasteryPacksPerTraveler(Range):
    """How many "Mastery: <Traveler>" items each Traveler has in the pool. 0 removes mastery from the pool.
    Every filler slot that isn't mastery is Stardust; 15 fills every filler slot with mastery."""
    display_name = "Mastery Packs per Traveler"
    range_start = 0
    range_end = 15
    default = 7


class MasteryPackValue(Range):
    """Mastery levels one "Mastery: <Traveler>" item gives that Traveler."""
    display_name = "Mastery Pack Value"
    range_start = 1
    range_end = 35
    default = 5


class StardustPackValue(Range):
    """Stardust one "Stardust" item gives."""
    display_name = "Stardust Pack Value"
    range_start = 1
    range_end = 10000
    default = 650


@dataclass
class SoDOptions(PerGameCommonOptions):
    goal_difficulty: GoalDifficulty
    goal_traveler_count: GoalTravelerCount
    mastery_packs_per_traveler: MasteryPacksPerTraveler
    mastery_pack_value: MasteryPackValue
    stardust_pack_value: StardustPackValue
    death_link: DeathLink


option_groups = [
    OptionGroup("Goal", [GoalDifficulty, GoalTravelerCount]),
    OptionGroup("Filler", [MasteryPacksPerTraveler, MasteryPackValue, StardustPackValue]),
]
