# @lusplit/application

Application orchestration layer over `@lusplit/core`.

- Commands + queries use-cases
- Ports (repositories, id generator, clock)
- Stable models contract for UI/API boundaries
- Core algorithms delegated to `@lusplit/core` (split, balances, settlement)
- Economic units may be created before the owner participant exists; owner-based balance/settlement queries require the owner participant to exist and belong to that unit.
