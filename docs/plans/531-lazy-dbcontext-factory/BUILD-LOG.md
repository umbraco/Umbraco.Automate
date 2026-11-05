# Build log

- **T1** `ab4ccc0b`: added `ResolvingPooledDbContextFactory`, `PlaceholderConnectionState` and the shared `AddAutomatePooledDbContext` helper. Core and OpenIddict build. `AmbientDbContextFactoryCompositionTests` (8) and OpenIddict `PooledDbContextOptionsTests` pass. Review PASS: the flag is always set before the wrapper reads it, checked against EF Core 10.0.10's source.
- **T2** `cf03bc57`: core registers through the shared helper. 14 S1-S3 specs active and green. Full suite green (2,391 unit, 286 integration). Review FAIL on six unused `using` lines; fixed in round 2, then PASS.
