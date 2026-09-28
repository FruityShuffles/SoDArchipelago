from collections import defaultdict
from random import Random
from typing import Dict, List, Optional, Tuple

from .data import GAME_DATA

# DESIGN.md "Shuffled star requirements": a star's group is its Traveler (None = the common stars, gated by total
# mastery) and its category. Requirements only move within a group, so every group keeps its vanilla set of levels.
star_groups: Dict[Tuple[Optional[str], str], List[dict]] = defaultdict(list)
for _star in GAME_DATA["stars"]:
    star_groups[(_star["traveler"], _star["category"])].append(_star)


def shuffle_star_requirements(random: Random) -> Dict[str, int]:
    """Star key -> its shuffled mastery requirement, for every star."""
    result: Dict[str, int] = {}
    for group in star_groups.values():
        levels = [star["required_level"] for star in group]
        random.shuffle(levels)
        result.update(zip((star["key"] for star in group), levels))
    return result
