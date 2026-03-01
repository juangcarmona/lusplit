# Agent Instructions (LuSplit)

You are working in a monorepo (apps/* + packages/*). Make changes in small PR-sized increments.

Before coding:
1) Identify the target slice (feature/package)
2) List files you will touch
3) Define acceptance criteria (tests + behavior)

Validation:
- Run typecheck and tests for affected packages.
- Never change Money semantics (minor units) without updating tests and export format.

Do not introduce network calls or backend dependencies in v1.