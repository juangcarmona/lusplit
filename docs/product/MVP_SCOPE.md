# LuSplit Product Phases

## Phase 1: MVP (Completed)

### Product Intent

LuSplit is a shared-expense clarity tool for families and friends.

### Delivered

- Group-level shared expense tracking
- Home view with group summary + expenses list
- Add expense flow with:
  - Simple form
  - Large inputs
  - People chips
  - Equal split default
- Clear owing/settled language in UI copy
- Dark mode support as a first-class mode
- Accessibility baseline:
  - Minimum `44px` touch targets
  - Responsive text scaling
  - `4.5:1` contrast minimum

### Experience Constraints (permanent)

- Calm, minimal interface with low visual noise
- Soft rounded component system
- Subtle motion only (`120–180ms`, ease-out cubic)
- No gamification mechanics

### Success Signal

Users can quickly log and split shared expenses, understand who owes what,
and reach a calm "all settled" resolution.

## Phase 2: Collaborative LuSplit

### Intent

Extend LuSplit from a single-device coordinator to a collaborative,
multi-device system without compromising local-first guarantees or calm UX.

### In Scope

- Optional accounts / device identity for sync participation
- Shared groups across devices
- Sync model: local-first with eventual consistency
- Offline remains fully functional — sync is additive
- Export formats remain unchanged

### Out of Scope (Phase 2)

- Real-time co-editing or presence
- Social features (profiles, avatars, activity feeds)
- Push notifications for expense updates
- Web client (deferred)

### Constraints

- Accounts must not gate core expense tracking
- Sync must not alter domain logic or balance computation
- Collaboration UX must preserve calm, low-noise interaction
- No fintech, budgeting, or banking positioning

### Success Signal

Multiple devices can share a group, add expenses independently, and converge
to the same balances without manual coordination.

## Permanent Exclusions

These are out of scope regardless of phase:

- Fintech positioning
- Budgeting workflows
- Banking features
- Heavy finance jargon and terminology
- Gamification mechanics
