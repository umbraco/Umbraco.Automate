# Build log

- **T1** `ab4ccc0b`: added `ResolvingPooledDbContextFactory`, `PlaceholderConnectionState` and the shared `AddAutomatePooledDbContext` helper. Core and OpenIddict build. `AmbientDbContextFactoryCompositionTests` (8) and OpenIddict `PooledDbContextOptionsTests` pass. Review PASS: the flag is always set before the wrapper reads it, checked against EF Core 10.0.10's source.
