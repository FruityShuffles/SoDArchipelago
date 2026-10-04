# Bookkeeping and delivery regression checks

Run from the repository root:

```sh
dotnet run --project mod/Tests/Tests.csproj
```

This standalone .NET 8 harness links the production Stardust allocation, ItemHandler, InRunItems, ProfileGuard and
CheckHandler and JonasWares source. Small game and network doubles exercise exact currency totals, batch/reconnect
replay, the constellation currency copy, delivery eligibility, pending counters, offline checks and resend, host-only
Jonas stock, vanilla pricing delegation, scouted item/recipient records, refresh and continued-save duplicate purchases.
No game is launched or deployed. This does not verify the vanilla effects implemented by issues #10–#12.
