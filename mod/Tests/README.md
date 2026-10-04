# Bookkeeping and delivery regression checks

Run from the repository root:

```sh
dotnet run --project mod/Tests/Tests.csproj
```

This standalone .NET 8 harness links the production Stardust allocation, ItemHandler, InRunItems, ProfileGuard and
CheckHandler source. Small game and network doubles exercise exact currency totals, batch/reconnect replay, the
constellation currency copy, delivery eligibility, pending counters and offline first-time checks and resend.
No game is launched or deployed. This does not verify the vanilla effects implemented by issues #10–#12.
