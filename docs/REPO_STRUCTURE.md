# Repository Structure

LuSplit uses Turborepo.

apps/
  mobile/     React Native
  web/        Next.js

packages/
  core/       Domain (pure TS)
  application/ CQRS + ports
  infra-local/ SQLite + export adapters
  ui/         Shared UI components (optional)

Domain must never depend on application or infra.
Application must depend only on domain.
Infra implements ports defined in application.
