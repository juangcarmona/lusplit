# Domain Model — LuSplit

LuSplit models shared expenses inside a Group and produces deterministic, auditable balances and settlement plans.

The domain is economic, not legal. It must work for friends, families, couples, schools, teams, clubs, delegations, or any human group.

---

## Core Concepts

### Group

A container for shared activity (trip, event, semester, project).

Rules:

* Single currency per group (v1)
* Closed groups are immutable (except export/import)

---

### Participant

A human entity participating in expenses.

A Participant:

* Can pay expenses
* Can owe money
* Belongs to exactly one Economic Unit within the Group

Attributes:

* `economicUnitId`
* `consumptionCategory`

  * `FULL` (default weight 1.0)
  * `HALF` (default weight 0.5)
  * `CUSTOM` (weight defined per expense or via explicit profile)

Notes:

* No legal semantics
* Economic grouping is handled exclusively through Economic Units

---

### Economic Unit

A settlement entity grouping one or more Participants under a single settlement responsibility.

Examples:

* A family settling as one entity
* A couple
* A sponsor covering a delegation
* A teacher paying for a class
* A company paying for employees

Properties:

* `id`
* `groupId`
* `name` (optional)
* `ownerParticipantId`

Rules:

* Every Participant belongs to exactly one Economic Unit
* Every Economic Unit has exactly one owner
* Owner must belong to its own unit
* A unit may contain only the owner (individual mode)

---

### Expense

Represents a financial event.

Properties:

* `id`
* `groupId`
* `title`
* `paidByParticipantId`
* `amountMinor` (integer, minor units)
* `date`
* `splitDefinition`
* `notes?`

Rules:

* Money is stored as integer minor units (never floats)
* The splitDefinition must fully determine share allocation
* Sum of all computed shares must equal `amountMinor`
* Split definition must be persisted exactly for auditability and stable re-import

---

# Split Model (Composed)

## SplitDefinition

An ordered list of SplitComponents applied sequentially to an expense amount.

Purpose:
Support flexible and mixed rules such as:

* "Alice pays exactly 7€, Bob pays 3€, remainder split equally"
* "Kids count as 50%"
* "This unit consumes as 3.5 people"
* "Only Alice, Bob and Charlie participate; the rest are excluded"

### Evaluation Model

1. Start with `remaining = expense.amountMinor`
2. Process components in order
3. Each component assigns shares and reduces `remaining`
4. After the final component:

   * `remaining` must equal 0
   * Otherwise the definition is invalid

Participants not referenced in any component:

* Owe 0 for that expense

---

## SplitComponent

Multiple components of the same type are allowed.

Each component must operate only on the current `remaining` amount.

---

### 1) FixedContribution

Assign fixed minor-unit amounts to participants.

Shape:

* `type: FIXED`
* `shares: { participantId -> amountMinor }`

Rules:

* `sum(shares) <= remaining`
* Shares are subtracted from remaining
* A participant may appear in multiple components; total share is the sum

Use cases:

* "Child pays 7€"
* "Bob pays 3€"
* Mixed fixed assignments

---

### 2) RemainderDistribution

Distributes the current remaining amount.

Shape:

* `type: REMAINDER`
* `participants: participantId[]`
* `mode: EQUAL | WEIGHT | PERCENT`
* `weights?`
* `percents?`

Rules:

* Operates on full current `remaining`
* After distribution, `remaining` must be 0
* Deterministic rounding required
* Stable participant ordering must be defined (e.g. lexicographical participantId)

Modes:

#### EQUAL

All listed participants share equally.

#### WEIGHT

Shares proportional to weight values.

Weights may:

* Be explicitly provided in the component
* Be derived from `consumptionCategory`

#### PERCENT

Explicit percentage per participant.
Sum must equal 100%.

---

## Participation Semantics

Participation is defined per expense via components.

A participant:

* May be excluded entirely
* May contribute fixed amount only
* May participate only in remainder distribution
* May participate in both fixed and remainder components

There is no implicit “everyone participates” rule.

---

# Balance Model

Balances are calculated per Participant.

For each Expense:

* If participant is payer: `balance += amountMinor`
* For each assigned share: `balance -= shareMinor`

Interpretation:

* Positive balance → participant should receive money
* Negative balance → participant owes money

Invariants:

* Sum of all participant balances = 0
* For each expense: sum of assigned shares = expense.amountMinor

---

# Aggregation Modes (Settlement Views)

LuSplit supports two aggregation views.

## Participant Mode

Balances and settlement computed per participant.

## Economic Unit Mode

Balances aggregated per Economic Unit:

* Sum balances of all participants in a unit
* Assign net result to the unit owner
* Settlement occurs between owners only

Aggregation affects presentation and settlement only.
Core balances always remain per participant.

---

# Transfer

Represents a settlement transaction.

Properties:

* `id`
* `groupId`
* `fromParticipantId`
* `toParticipantId`
* `amountMinor`
* `date`
* `type: GENERATED | MANUAL`
* `note?`

Transfers reduce balances but never alter historical expenses.

---

# Determinism Requirements

* All monetary values are integer minor units
* No floating-point arithmetic
* Stable deterministic rounding
* Split evaluation order is fixed and reproducible
* Same inputs must always produce the same balances and settlement plan

---

# Out of Scope (v1)

* Multi-currency
* Identity/authentication
* Sync/collaboration
* Legal guardianship semantics
* Tax/VAT modeling
* Cross-group dependencies
