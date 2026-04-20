# Research: Shared Synchronized Groups

**Feature**: 001-shared-sync-groups
**Date**: 2026-04-18

## Research Tasks

### 1. Identity Provider: Microsoft Entra External ID for Consumer Mobile Apps

**Decision**: Use Microsoft Entra External ID (CIAM) with MSAL.NET for user authentication.

**Rationale**:
- Entra External ID is Microsoft's current consumer identity platform, replacing Azure AD B2C (no longer available to new customers as of May 2025).
- Supports self-service sign-up, social identity providers (Google, Apple, Facebook), email/password, and one-time passcode.
- Provides OAuth 2.0 / OIDC flows suitable for mobile apps, including PKCE-based authorization code flow.
- MSAL.NET handles token acquisition, caching, and refresh transparently on MAUI platforms.
- Free tier covers up to 50,000 monthly active users — well above the baseline target.
- MFA can be layered via Conditional Access policies without app-level changes.

**Alternatives considered**:
- **Auth0 / Okta**: Mature consumer identity platforms but introduce a non-Azure vendor dependency and separate billing. Rejected to stay within Azure-only constraint.
- **Firebase Auth**: Cross-platform but Google-ecosystem-centric. Rejected for Azure-only constraint.
- **Custom JWT issuer**: Maximum flexibility but massive security surface to own. Rejected as unnecessary complexity.

**Open risks**: Entra External ID pricing for premium features (M2M, advanced MFA) should be validated before production commitment.

---

### 2. Delegated Storage Access: User Delegation SAS Tokens

**Decision**: Use Azure Blob Storage User Delegation SAS tokens scoped to per-group containers, issued by the Azure Functions control plane.

**Rationale**:
- User Delegation SAS is signed with Entra credentials (not account keys), so no storage account keys need to exist in client code or control-plane configuration.
- SAS tokens can be scoped to a specific container with read-only or read-write permissions and short expiration (e.g., 15 minutes).
- The Azure Functions control plane verifies group membership, then calls `GetUserDelegationKey` + `BlobSasBuilder` to mint a scoped SAS.
- The client uses the SAS URI directly to upload/download encrypted blobs — no proxy traffic through the Functions layer.
- User Delegation Key is valid up to 7 days; SAS tokens minted from it can have shorter lifetimes.

**Alternatives considered**:
- **Account key SAS**: Simpler but requires the account key in the Functions environment, violating the "no long-lived infrastructure secrets" goal. Rejected.
- **Managed Identity + proxy**: All traffic routes through Functions. Eliminates SAS complexity but creates a bottleneck, increases Functions cost, and violates the "thin control plane" goal. Rejected.
- **Azure Data Lake Storage Gen2 ACLs**: POSIX-style ACLs per-path but adds complexity and doesn't eliminate the need for SAS for mobile clients. Rejected.

---

### 3. Secure Key Storage on Device: MAUI SecureStorage

**Decision**: Use `Microsoft.Maui.Storage.SecureStorage` for storing device-bound private keys and wrapped group keys.

**Rationale**:
- MAUI SecureStorage is a cross-platform API backed by:
  - Android: EncryptedSharedPreferences (AES-256 + Android Keystore)
  - iOS/macOS: Keychain Services
  - Windows: DataProtectionProvider
- No additional platform-specific setup required on Android.
- Simple key/value API (`SetAsync`, `GetAsync`, `Remove`) sufficient for storing serialized key material.
- Keys stored here are not exportable to other apps or accessible after device revocation (once the app data is cleared).

**Limitations**:
- Designed for small values (tokens, keys) — not bulk data storage. Appropriate for our use case.
- On Windows unpackaged apps, values are stored in a local JSON file with DataProtection encryption — acceptable for dev/test; production targets are mobile.

**Alternatives considered**:
- **Platform-native APIs directly** (Keychain, Keystore): More control but requires per-platform code. Rejected for maintainability — MAUI abstracts this cleanly.
- **Third-party secure storage library**: No established .NET MAUI library offers significantly better security than the built-in option. Rejected.

---

### 4. Client-Side Encryption: AES-256-GCM with Asymmetric Key Wrapping

**Decision**: Encrypt group content with AES-256-GCM. Wrap group keys per-device using RSA-OAEP (or ECDH-ES if supported). Use `System.Security.Cryptography` for all crypto operations.

**Rationale**:
- AES-256-GCM provides authenticated encryption (confidentiality + integrity) in a single pass.
- `System.Security.Cryptography` ships with .NET and supports AES-GCM on all target platforms.
- RSA-OAEP key wrapping is well-understood, has broad .NET support, and fits the per-device public key model.
- Each device generates an RSA keypair on first registration. The public key is uploaded to the control plane. The private key stays in SecureStorage.
- When the control plane needs to deliver a group key to a device, it wraps the group key with the device's public key. Only that device can unwrap it.

**Alternatives considered**:
- **libsodium / NaCl via Sodium.Core**: Simpler API for crypto but adds a native dependency. `System.Security.Cryptography` is sufficient and avoids the dependency. Rejected.
- **ECDH-ES key agreement**: More modern than RSA-OAEP but requires both parties' public keys at handshake time. RSA-OAEP is simpler for the asynchronous "control plane wraps for absent device" pattern. May be reconsidered in a hardening phase.

---

### 5. Sync Protocol: Operation Log with Hybrid Logical Clocks

**Decision**: Use an append-only operation log with Hybrid Logical Clock (HLC) timestamps for ordering. Operations are encrypted and stored as individual blobs in the group's Blob Storage container.

**Rationale**:
- HLC provides causal ordering across devices without requiring synchronized wall clocks. Each operation's timestamp is `max(local_HLC, last_known_remote_HLC) + 1`.
- Operations are immutable and append-only — no in-place updates to remote blobs.
- Conflict resolution uses last-write-wins on the HLC timestamp for same-field edits. Additions and independent edits are commutative.
- Snapshots (every 100 operations) serve as compaction checkpoints. New devices bootstrap from the latest snapshot + subsequent operations.
- Blob naming convention: `ops/{HLC_timestamp}_{device_id}.enc` for operations, `snapshots/{HLC_timestamp}.enc` for snapshots.

**Alternatives considered**:
- **CRDTs (Conflict-free Replicated Data Types)**: Theoretically superior convergence but significant implementation complexity for the LuSplit data model (expenses with splits, transfers, participants). The domain model is not naturally CRDT-shaped. Rejected for baseline; may be reconsidered if LWW proves insufficient.
- **Event sourcing with a central log**: Requires a durable ordered log service (e.g., Event Hubs). Violates the minimal infrastructure constraint. Rejected.
- **Full-state sync (upload entire group on every change)**: Simple but wasteful for large groups and creates last-write-wins at the group level, losing concurrent additions. Rejected.

---

### 6. Azure Functions: Isolated Worker Model on Consumption Plan

**Decision**: Use Azure Functions (.NET 10, isolated worker model) on the Consumption plan.

**Rationale**:
- Isolated worker model is the recommended approach for .NET Functions going forward. Supports .NET 10.
- Consumption plan: pay-per-execution, scales to zero when idle, aligns with cost target.
- Functions act as HTTP-triggered endpoints for: device registration, invitation lifecycle, authorization checks, SAS token issuance, wrapped key distribution.
- Managed Identity for the Functions app provides access to Key Vault secrets and Blob Storage (for generating User Delegation SAS), eliminating stored secrets.

**Alternatives considered**:
- **Flex Consumption**: Better cold-start characteristics but higher baseline cost. Consumption plan is sufficient for the expected invocation volume (~150K/day). May upgrade later if cold starts become a UX issue.
- **Azure Container Apps**: More flexible but higher operational overhead. Rejected for minimal infrastructure goal.

---

### 7. Infrastructure as Code: Bicep with Module-Per-Resource Pattern

**Decision**: Use Bicep with one module per logical resource group (Functions, Storage, Key Vault, Identity, Monitoring). Orchestrated by a top-level `main.bicep` with environment-specific `.bicepparam` files.

**Rationale**:
- Bicep is Azure-native, has first-class VS Code tooling, and is the recommended IaC for Azure resources.
- Module-per-resource pattern keeps each resource definition focused and testable.
- `.bicepparam` files for dev/prod allow environment promotion without template duplication.
- `az bicep build` can validate templates in CI before deployment.

**Alternatives considered**:
- **Terraform**: More portable but adds a provider dependency and HCL syntax unfamiliar to the existing .NET-focused codebase. Rejected per spec preference for Bicep unless a hard limitation is found. No hard limitation was found.
- **ARM templates directly**: Verbose and harder to maintain. Bicep compiles to ARM and is strictly superior for authoring. Rejected.
- **Pulumi (C#)**: Appealing for a .NET team but adds a runtime dependency and SDK. Overkill for the small resource set. Rejected.

---

### 8. Blob Storage Container and Path Conventions

**Decision**: One container per shared group, named `group-{groupId}`. Operations and snapshots stored as blobs within the container.

**Rationale**:
- Per-group containers allow SAS tokens to be scoped to exactly one group, enforcing isolation at the storage level.
- Blob path convention:
  - `ops/{hlc_timestamp}_{deviceId}.enc` — encrypted operation blobs
  - `snapshots/{hlc_timestamp}.enc` — encrypted snapshot blobs
  - `meta/membership.enc` — encrypted membership metadata (for bootstrapping)
- Container creation is handled by the control plane when a group is shared.
- Lifecycle policies can be applied per-container for snapshot retention.

**Alternatives considered**:
- **Single container with path prefixes**: Simpler container management but SAS tokens cannot be scoped to a path prefix — only to a container or individual blob. This would require broader SAS permissions than desired. Rejected.
- **Hierarchical namespace (ADLS Gen2)**: Supports directory-level ACLs but adds cost and complexity. Rejected for baseline.
