# LuSplit

Open-source, offline-first expense splitting for friends and families.

Clear balances. No accounts. No paywalls.

---

## Why LuSplit exists

Every winter, Lucía organizes a ski trip with her family.

Apartments. Groceries. Lift passes. Gas. Restaurants.  
Lots of shared expenses. Lots of people. Kids included.

For years she used Splitwise. Then Tricount.  
Both were simple. Both were great.

Until basic features quietly moved behind paywalls.

This year, she invited us to join their trip.  
I had an accident. Tore my knee. Could barely walk.  
Lucía drove us home — 700 km — without hesitation.

So this is a small return gesture.

LuSplit is a gift to her.

And to everyone who just wants to split expenses clearly,
without subscriptions, without friction, without losing access
to their own data.

---

## Principles

- Offline-first
- No mandatory accounts
- No freemium traps
- Your data stays on your device
- Export everything anytime
- Open source

If we ever introduce ads, it will only be to sustain
optional backend infrastructure — never to lock core features.

---

## What LuSplit does (v1)

- Create trips or groups
- Add members
- Add dependent members (kids linked to guardians)
- Split expenses equally, exactly, by percentage or by weight
- Calculate balances
- Generate a simple settlement plan
- Export your trip (JSON / CSV / PDF)

---

## Architecture

LuSplit is built as a monorepo:

- `core` – domain logic (pure TypeScript, deterministic money handling)
- `application` – CQRS commands & queries
- `infra-local` – local persistence (SQLite on mobile)
- `mobile` – React Native app
- `web` – web client (planned)

All money is stored in minor units (cents).  
All calculations are deterministic and tested.

---

## Roadmap

### v1
- Offline mobile app
- Local database
- Export snapshot
- Family-friendly splitting

### v2
- Optional sync backend
- Multi-device
- Collaborative groups

---

## License

MIT

Use it. Fork it. Improve it.

---

Made for Lucía.
