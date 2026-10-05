# Bookkeeping and delivery regression checks

Run from the repository root:

```sh
dotnet run --project mod/Tests/Tests.csproj
```

This standalone .NET 8 harness links the production Stardust allocation, ItemHandler, InRunItems, ProfileGuard and
CheckHandler and JonasWares source. Small game and network doubles exercise exact currency totals, batch/reconnect
replay, the constellation currency copy, delivery eligibility, pending counters, offline checks and resend, host-only
Jonas stock, vanilla pricing delegation, scouted item/recipient records, refresh and continued-save duplicate purchases.
WareShopUi coverage includes a cached AP cell reused at another shop or on a vanilla profile, literal TMP tooltip names
and the final discounted price and availability. Installed TMP parsing is verified separately against its decompiled source.
Pilgrimage coverage includes the local shrine user, quest failure/late completion state, joining-client offline checks,
native artifact hand-in flags, duplicate discovery, resend, and vanilla/other/failed/transient profile guards.
The production MapBlessings, Treasures, CurseTraps and DeathLinkHandler sources are also linked. Coverage includes
map targeting and pings, Treasure eligibility and free spawn fields, Clairvoyance cleanup, Shard/DeathLink suppression,
all three curse tiers, curse pool filtering and full-asset resolution, kill/travel conditions and world scaling,
boss-room deferral, per-copy counters and restart replay prevention.
Blessing regressions cover exit-boss/sidetrack/special-map deferral, invalid room/node state, identical shrine
restore collisions, distinct shrines sharing a node, and Artifact delivery after a party hand-in.
Treasure regressions cover exit-boss deferral with Shards still enabled, sidetrack/special-map deferral, and delayed
native map Treasure/quest initialization. Both map kinds wait for queued effects without deduplicating initialized
quests or blocking other Treasures; Clairvoyance cleanup forces its queued reveal before the next copy is checked.
No game is launched or deployed. The doubles verify AP delivery and delegation; decompiled code and installed
prefabs are inspected separately to verify the vanilla effects.
