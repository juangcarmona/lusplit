# @lusplit/infra-mobile

React Native / Expo-compatible local persistence adapter.

This package implements application ports using Expo SQLite while preserving:

- schema semantics and constraints from `@lusplit/infra-local`
- deterministic ordering guarantees
- snapshot export/import behavior
