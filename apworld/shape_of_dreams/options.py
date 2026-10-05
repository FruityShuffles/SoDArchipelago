from dataclasses import dataclass

from Options import Choice, DeathLink, DefaultOnToggle, OptionGroup, OptionSet, PerGameCommonOptions, Range, Toggle

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
    You start with 2 random Travelers; the others must be received first."""
    display_name = "Goal Traveler Count"
    range_start = 1
    range_end = 9
    default = 9


class MasteryPacksPerTraveler(Range):
    """How many "Mastery: <Traveler>" items each Traveler has in the pool. 0 removes mastery from the pool.
    Mastery items are always useful; remaining baseline slots stay Stardust."""
    display_name = "Mastery Packs per Traveler"
    range_start = 0
    range_end = 15
    default = 8


class MasteryPackValue(Range):
    """Mastery levels one "Mastery: <Traveler>" item gives that Traveler."""
    display_name = "Mastery Pack Value"
    range_start = 1
    range_end = 40
    default = 5


class StardustTotal(Range):
    """Total Stardust from the seed's Stardust items, divided evenly with the remainder spread exactly.
    Changing the number of Stardust items does not change the total income."""
    display_name = "Stardust Total"
    range_start = 0
    range_end = 1000000
    default = 54000


class InRunItems(Toggle):
    """Fill new location slots with Map Blessings and Treasures. They land during solo or hosted runs;
    if you join another player's game, they wait until you play solo or host."""
    display_name = "In-run Items"


class Traps(Toggle):
    """Fill 20% of new location slots with Curse traps. They land during solo or hosted runs."""
    display_name = "Traps"


class JonasWares(Range):
    """Number of Archipelago wares sold by Jonas. Wares require solo play or hosting.
    Set to 0 if you mostly join other people's games."""
    display_name = "Jonas's Wares"
    range_start = 0
    range_end = 100
    default = 30


class PassiveMastery(DefaultOnToggle):
    """Runs earn Traveler mastery as in vanilla. When off, runs earn no mastery, so mastery only comes from
    "Mastery: <Traveler>" items, and "Achievement: The Road Not Taken" (which needs a Traveler at mastery 40) is
    excluded."""
    display_name = "Passive Mastery"


# These descriptions appear in generated YAML templates. Individual dream effects belong beside their names.
class ForcedEvilLucidDreams(OptionSet):
    """List of Evil Lucid Dream names forced until their release items arrive. An empty list selects none.
    Accepted names and effects:
    Fish Scales: Monster damage also lowers your maximum health until the next world.
    Grievous Wounds: Halves healing and shields on Travelers.
    Mad Life: Monsters predict your movement.
    Marsh of Destiny: Each world offers a Seed of Torment shrine: rewards for strengthening monsters.
    Overpopulation: Adds 50% more monsters, excluding bosses.
    Prudent Jellyfish: Weakens cooldown reduction and limits how much cooldowns can be shortened.
    For forced-dream rules, see the game info page:
    https://github.com/FruityShuffles/SoDArchipelago/blob/main/apworld/shape_of_dreams/docs/en_Shape%20of%20Dreams.md#forced-lucid-dreams"""
    display_name = "Forced Evil Lucid Dreams"
    valid_keys = sorted(lucid_dreams_by_type["evil"])
    default = frozenset()


class ForcedChaoticLucidDreams(OptionSet):
    """List of Chaotic Lucid Dream names forced until their release items arrive. An empty list selects none.
    Accepted names and effects:
    Embrace Mortality: Travelers and monsters deal double damage.
    Harmless Whispers: Bosses can be crowd-controlled, but gain health, damage, tenacity
    and shields against repeated control.
    Sparkling Dream Flask: Health potions and Guidance shrines give Dream Dust instead of healing.
    The Darkest Urge: Monsters fight each other and grow stronger when they kill another monster.
    WILD: Every monster except bosses is a Hunter.
    For forced-dream rules, see the game info page:
    https://github.com/FruityShuffles/SoDArchipelago/blob/main/apworld/shape_of_dreams/docs/en_Shape%20of%20Dreams.md#forced-lucid-dreams"""
    display_name = "Forced Chaotic Lucid Dreams"
    valid_keys = sorted(lucid_dreams_by_type["chaotic"])
    default = frozenset()


class ShuffleStarRequirements(DefaultOnToggle):
    """Shuffles the mastery level each constellation star needs before you can buy it. Stars only trade levels with
    stars of the same Traveler (or the common stars) and the same category, so each group keeps its usual levels."""
    display_name = "Shuffle Star Requirements"


@dataclass
class SoDOptions(PerGameCommonOptions):
    goal_difficulty: GoalDifficulty
    goal_traveler_count: GoalTravelerCount
    mastery_packs_per_traveler: MasteryPacksPerTraveler
    mastery_pack_value: MasteryPackValue
    stardust_total: StardustTotal
    in_run_items: InRunItems
    traps: Traps
    jonas_wares: JonasWares
    passive_mastery: PassiveMastery
    forced_evil_lucid_dreams: ForcedEvilLucidDreams
    forced_chaotic_lucid_dreams: ForcedChaoticLucidDreams
    shuffle_star_requirements: ShuffleStarRequirements
    death_link: DeathLink


option_groups = [
    OptionGroup("Goal", [GoalDifficulty, GoalTravelerCount]),
    OptionGroup("Mastery and Stardust", [MasteryPacksPerTraveler, MasteryPackValue, StardustTotal, PassiveMastery]),
    OptionGroup("In-run Content", [InRunItems, Traps, JonasWares]),
    OptionGroup("Forced Lucid Dreams", [ForcedEvilLucidDreams, ForcedChaoticLucidDreams]),
    OptionGroup("Constellation", [ShuffleStarRequirements]),
]
