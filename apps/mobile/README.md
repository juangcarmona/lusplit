# LuSplit Mobile (Expo)

Minimal local-first Android MVP using Expo managed workflow, Redux Toolkit, TypeScript, and application-layer use-cases.

## Architecture rules

- UI calls command/query use-cases from `@lusplit/application`
- Domain logic remains in `@lusplit/core`
- Local persistence is `@lusplit/infra-mobile` (Expo SQLite)
- No split or settlement math in Redux reducers/components

## Prerequisites

- Node `24.13.1` for repository baseline
- Android Studio + Android Emulator
- pnpm `9.x`

If Expo tooling has local Node friction, use an app-local Node override for development, but keep repository baseline unchanged.

## Install

From repo root:

```bash
pnpm install
```

## Run on Android emulator

From repo root:

```bash
pnpm --filter @lusplit/mobile start
```

Then press `a` in the Expo terminal, or run:

```bash
pnpm --filter @lusplit/mobile android
```

## Typecheck

```bash
pnpm typecheck
```

## Manual DoD scenario

1. Create a group from **Groups**.
2. Open group detail.
3. Add participants (each action creates economic unit + owner participant).
4. Add expenses with mixed split modes in **Add Expense**:
	- `EQUAL`
	- `FIXED_REMAINDER`
	- `WEIGHT`
5. Open **Balances** and verify participant/owner modes.
6. Open **Settlement** and verify participant/owner modes.
7. Back in **Group Detail**, export snapshot JSON.
8. Reset app data.
9. Import snapshot JSON into fresh state.
10. Re-open **Balances** and **Settlement**; values and ordering must match pre-export.

## Current scope notes

- Export/import currently uses JSON text in-app (copy/paste flow).
- This is MVP functionality-first UI, not design polish.
