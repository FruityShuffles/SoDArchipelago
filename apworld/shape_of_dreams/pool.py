"""Exact, deterministic allocation of the new slots; baseline filler stays Stardust."""
from typing import Dict

from .items import STARDUST, curse_item_weights, in_run_item_weights


def largest_remainder(count: int, weights: Dict[str, int]) -> Dict[str, int]:
    if count < 0 or not weights or any(weight <= 0 for weight in weights.values()):
        raise ValueError("Allocation needs a nonnegative count and positive weights")
    total = sum(weights.values())
    counts = {name: count * weight // total for name, weight in weights.items()}
    # Stable sort preserves table order on equal remainders. Use integers, never floating-point approximations.
    order = sorted(weights, key=lambda name: count * weights[name] % total, reverse=True)
    for name in order[:count - sum(counts.values())]:
        counts[name] += 1
    return counts


def new_slot_counts(count: int, in_run_items: bool, traps: bool) -> Dict[str, int]:
    share = largest_remainder(count, {"curse": 1, "rest": 4}) if traps else {"curse": 0, "rest": count}
    counts = largest_remainder(share["curse"], curse_item_weights)
    if in_run_items:
        counts.update(largest_remainder(share["rest"], in_run_item_weights))
    else:
        counts[STARDUST] = share["rest"]
    return counts
