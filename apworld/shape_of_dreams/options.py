from dataclasses import dataclass

from Options import Choice, DeathLink, OptionGroup, OptionSet, PerGameCommonOptions, Range

from .items import lucid_dreams_by_type


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


# The dream descriptions below are also in examples/Shape of Dreams.yaml; keep both in step.
class ForcedEvilLucidDreams(OptionSet):
    """Evil Lucid Dreams that are forced on in every run until you receive their "Lucid Dream: <name>" item.
    After that you can turn them on or off as usual. These items become progression.
    A run only counts for world clears and your goal if every still-forced dream is active.

    Fish Scales: damage from monsters also lowers your max health until the next world (30% of it, 40% from bosses,
    less for melee Travelers). Guidance shrines restore some. Shields can't exceed your max health.
    Grievous Wounds: healing and shields on Travelers are halved.
    Mad Life: monsters predict your movement and aim where you're going.
    Marsh of Destiny: each world starts with a Seed of Torment shrine that offers rewards in exchange for making monsters
    stronger (health, armor, speed, Mirage Skins, curses, boss skills).
    Overpopulation: 50% more monsters (bosses excluded).
    Prudent Jellyfish: cooldown reduction is half as strong, and ability haste and Memory upgrades can shorten a
    cooldown by at most half."""
    display_name = "Forced Evil Lucid Dreams"
    valid_keys = sorted(lucid_dreams_by_type["evil"])
    default = frozenset()


class ForcedChaoticLucidDreams(OptionSet):
    """Chaotic Lucid Dreams that are forced on in every run until you receive their "Lucid Dream: <name>" item.
    After that you can turn them on or off as usual. These items become progression.
    A run only counts for world clears and your goal if every still-forced dream is active.

    Embrace Mortality: everyone deals double damage, Travelers and monsters alike.
    Harmless Whispers: bosses can be crowd-controlled, but get +15% health, +10% damage and extra tenacity, and shield
    themselves when controlled too often.
    Sparkling Dream Flask: health potions and Guidance shrines give Dream Dust instead of healing.
    The Darkest Urge: monsters fight each other. A monster that kills another levels up: more health, damage and speed,
    and a full heal.
    WILD: every monster except bosses is a Hunter."""
    display_name = "Forced Chaotic Lucid Dreams"
    valid_keys = sorted(lucid_dreams_by_type["chaotic"])
    default = frozenset()


@dataclass
class SoDOptions(PerGameCommonOptions):
    goal_difficulty: GoalDifficulty
    goal_traveler_count: GoalTravelerCount
    mastery_packs_per_traveler: MasteryPacksPerTraveler
    mastery_pack_value: MasteryPackValue
    stardust_pack_value: StardustPackValue
    forced_evil_lucid_dreams: ForcedEvilLucidDreams
    forced_chaotic_lucid_dreams: ForcedChaoticLucidDreams
    death_link: DeathLink


option_groups = [
    OptionGroup("Goal", [GoalDifficulty, GoalTravelerCount]),
    OptionGroup("Filler", [MasteryPacksPerTraveler, MasteryPackValue, StardustPackValue]),
    OptionGroup("Forced Lucid Dreams", [ForcedEvilLucidDreams, ForcedChaoticLucidDreams]),
]
