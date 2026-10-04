# Bookkeeping and delivery regression checks

Run from the repository root:

```sh
dotnet run --project mod/Tests/Tests.csproj
```

This standalone .NET 8 harness links the production Stardust allocation, ItemHandler, InRunItems, ProfileGuard and
CheckHandler and JonasWares source. Small game and network doubles exercise exact currency totals, batch/reconnect
replay, the constellation currency copy, delivery eligibility, pending counters, offline checks and resend, host-only
Jonas stock, vanilla pricing delegation, scouted item/recipient records, refresh and continued-save duplicate purchases.
Pilgrimage coverage includes the local shrine user, quest failure/late completion state, joining-client offline checks,
native artifact hand-in flags, duplicate discovery, resend, and vanilla/other/failed/transient profile guards.
The production MapBlessings, Treasures, CurseTraps and DeathLinkHandler sources are also linked. Coverage includes
map targeting and pings, Treasure eligibility and free spawn fields, Clairvoyance cleanup, Shard/DeathLink suppression,
all three curse tiers, curse pool filtering and full-asset resolution, kill/travel conditions and world scaling,
boss-room deferral, per-copy counters and restart replay prevention.
No game is launched or deployed. The doubles verify AP delivery and delegation; decompiled code and installed
prefabs are inspected separately to verify the vanilla effects.
