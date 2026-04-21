# LuSplit Shared Sync Groups — DEV Deployment README

This document explains how to prepare the **DEV** environment for the shared synchronized groups feature for dev environment:

- Azure resources are provisioned with **Bicep**.
- **Microsoft Entra External ID** and **app registrations** are created **manually**.
- Sensitive values are stored in **GitHub Secrets** and in **local developer secrets/config**.

This README is for the current solution shape:

- Azure Functions as the control plane
- Azure Blob Storage for encrypted operations and snapshots
- Key Vault for secrets
- Application Insights / Log Analytics for monitoring
- Microsoft Entra External ID for authentication

---

## 1. Scope of this README

This README covers:

1. What to create manually in Azure / Entra
2. What to deploy with Bicep
3. What values to inject into GitHub Secrets
4. What values to inject into local/dev configuration
5. How to publish the Functions app
6. How to run a minimal DEV smoke test

This README assumes the feature is already implemented and the goal is to **bootstrap a usable DEV environment**, not redesign the system.

---

## 2. Current infra shape and one important limitation

The current `infra/main.bicep` deploys:

- Storage account
- Key Vault
- Monitoring (Log Analytics + App Insights)
- Azure Function App on Consumption plan

The current identity module **does not provision Entra External ID or app registrations**. It explicitly documents that CIAM provisioning is manual.

Because of that, DEV setup is a **hybrid flow**:

- **Manual**: Entra External ID tenant / app registrations / scopes / redirect URIs
- **Automated**: Azure resource group resources through Bicep

---

## 3. Prerequisites

### Local tools

Install and verify:

- Azure CLI
- Azure Functions Core Tools
- .NET SDK 10
- PowerShell 7+

Recommended checks:

```powershell
az version
func --version
dotnet --version
$PSVersionTable.PSVersion
```

### Azure access

You need:

- Access to the Azure subscription that will host DEV
- Permission to create resource groups, storage, Functions, Key Vault, monitoring, and RBAC assignments
- Permission to create or manage app registrations in the Entra External ID tenant used by the app

### Repo assumptions

Run commands from the repo root unless noted.

---

## 4. Naming used in DEV

Suggested DEV naming:

- Resource group: `lusplit-rg-dev`
- App name: `lusplit`
- Environment name: `dev`
- Region: `spaincentral`

With the current Bicep files, expected resource names are roughly:

- Storage account: `lusplitdevstorage`
- Function App: `lusplit-dev-fn`
- Plan: `lusplit-dev-plan`
- Key Vault: `lusplit-dev-kv`
- Log Analytics: `lusplit-dev-logs`
- App Insights: `lusplit-dev-ai`

---

## 5. Manual Azure / Entra setup

This part is **manual by design**.

### 5.1 Create or identify the Entra External ID tenant

Use an existing DEV CIAM tenant or create one for LuSplit DEV.

You need the following values from that tenant:

- `ExternalTenantId`
- The CIAM authority host you will use
- The mobile/public client registration ID
- The API/control-plane registration ID

Keep a record of:

- Tenant ID
- Tenant domain / authority base
- Mobile client ID
- API client ID / application ID URI / exposed scope

---

### 5.2 Create the mobile app registration

Create a **public client** app registration for the MAUI app.

Configure at least:

- Platform support for mobile/public client flows
- Redirect URIs for the MAUI targets you will test in DEV
- PKCE / authorization code flow support
- Permission to call the LuSplit control-plane API scope

Capture these values:

- `MobileClientId`
- Redirect URI(s)
- Scope(s) requested by the MAUI app

Notes:

- Android redirect URI typically follows the MSAL pattern for package/signature-based callbacks.
- iOS/macOS/Windows redirect URIs depend on the MSAL registration style you use in the app.
- Keep DEV and PROD registrations separate.

---

### 5.3 Create the API / control-plane app registration

Create the app registration used to validate bearer tokens for the Functions control plane.

Configure at least:

- Expose an API / define an audience
- Create one delegated scope for the mobile app to call the control plane
- Grant the mobile app permission to request that scope

Capture these values:

- `ApiClientId`
- `ApiAudience` or Application ID URI
- `ApiScope` used by the mobile app

---

### 5.4 Decide the DEV deep link base

The invitation flow needs a stable DEV deep link base.

Decide one of these:

- a custom scheme, or
- an app/universal link domain for DEV

Capture:

- `InviteBaseUrl`

Examples:

- `lusplit://invite`
- `https://dev.lusplit.example/invite`

Use whatever your current app implementation expects.

---

## 6. One-time infra fixes before first real deploy

The current Bicep is enough to create the Azure resource skeleton, but it is **not yet enough to fully wire auth settings end-to-end**.

Before relying on it, apply these minimal fixes in the repo.

### 6.1 Extend `infra/main.bicep` with identity parameters

Add these parameters to `infra/main.bicep`:

```bicep
param externalTenantId string = ''
param apiClientId string = ''
param mobileClientId string = ''
param authority string = ''
```

Pass them into the `identity` module:

```bicep
module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    externalTenantId: externalTenantId
    apiClientId: apiClientId
    mobileClientId: mobileClientId
  }
}
```

### 6.2 Surface identity outputs from `main.bicep`

Add outputs such as:

```bicep
output externalTenantId string = identity.outputs.externalTenantId
output apiClientId string = identity.outputs.apiClientId
output mobileClientId string = identity.outputs.mobileClientId
output authority string = identity.outputs.authority
```

### 6.3 Inject identity settings into the Function App

Update `infra/modules/functions.bicep` so it can receive and publish auth-related app settings.

Add parameters:

```bicep
param externalTenantId string = ''
param apiClientId string = ''
param mobileClientId string = ''
param authority string = ''
```

Then pass them from `main.bicep` into the functions module.

Finally, add app settings on the Function App for the values your Functions project actually reads. Example placeholders:

```bicep
{ name: 'Entra__TenantId', value: externalTenantId }
{ name: 'Entra__ApiClientId', value: apiClientId }
{ name: 'Entra__MobileClientId', value: mobileClientId }
{ name: 'Entra__Authority', value: authority }
```

Use the exact names expected by `EntraTokenValidationMiddleware` and your Function host configuration.

### 6.4 Add identity values to `infra/parameters/dev.bicepparam`

After extending `main.bicep`, update DEV params:

```bicep
using '../main.bicep'

param location = 'spaincentral'
param environmentName = 'dev'
param appName = 'lusplit'
param externalTenantId = '<DEV_CIAM_TENANT_ID>'
param apiClientId = '<DEV_API_CLIENT_ID>'
param mobileClientId = '<DEV_MOBILE_CLIENT_ID>'
param authority = '<DEV_CIAM_AUTHORITY>'
```

### 6.5 Review Function host storage configuration

The Function App currently sets:

- `AzureWebJobsStorage__accountName`

If your isolated worker setup needs additional storage settings for host startup in Azure, add them before treating DEV as final.

Do not guess. Match the configuration pattern already used by the Functions runtime in the project.

---

## 7. Create DEV resource group

Run in PowerShell:

```powershell
$SubscriptionId = '<YOUR_AZURE_SUBSCRIPTION_ID>'
$Location       = 'spaincentral'
$ResourceGroup  = 'lusplit-rg-dev'

az login
az account set --subscription $SubscriptionId
az group create --name $ResourceGroup --location $Location
```

---

## 8. Deploy the DEV infrastructure with Bicep

### 8.1 Validate Bicep

```powershell
az bicep build --file .\infra\main.bicep
```

### 8.2 Deploy

```powershell
$ResourceGroup = 'lusplit-rg-dev'

az deployment group create `
  --resource-group $ResourceGroup `
  --template-file .\infra\main.bicep `
  --parameters .\infra\parameters\dev.bicepparam
```

### 8.3 Read outputs

```powershell
$DeploymentName = (az deployment group list --resource-group $ResourceGroup --query "[0].name" -o tsv)

az deployment group show `
  --resource-group $ResourceGroup `
  --name $DeploymentName `
  --query properties.outputs
```

Capture at least:

- `functionAppName`
- `storageAccountName`
- `keyVaultName`
- `functionAppPrincipalId`
- `authority` if you exposed it

---

## 9. Post-deploy RBAC verification

The Functions app needs working RBAC for:

- Key Vault secret reads
- Blob access needed by the control plane

The current Bicep already assigns:

- Key Vault Secrets User on the vault
- Storage Blob Data Contributor on the storage account

Still verify it explicitly after deploy.

### 9.1 Inspect current role assignments

```powershell
$ResourceGroup = 'lusplit-rg-dev'
$FunctionAppName = 'lusplit-dev-fn'

$PrincipalId = az functionapp identity show `
  --resource-group $ResourceGroup `
  --name $FunctionAppName `
  --query principalId -o tsv

$StorageAccountName = az storage account list `
  --resource-group $ResourceGroup `
  --query "[0].name" -o tsv

$KeyVaultName = az keyvault list `
  --resource-group $ResourceGroup `
  --query "[0].name" -o tsv

az role assignment list --assignee $PrincipalId -o table
```

### 9.2 Add missing assignments manually if needed

If a role assignment is missing, add it explicitly.

#### Storage Blob Data Contributor

```powershell
$StorageId = az storage account show `
  --resource-group $ResourceGroup `
  --name $StorageAccountName `
  --query id -o tsv

az role assignment create `
  --assignee-object-id $PrincipalId `
  --assignee-principal-type ServicePrincipal `
  --role "Storage Blob Data Contributor" `
  --scope $StorageId
```

#### Key Vault Secrets User

```powershell
$KeyVaultId = az keyvault show `
  --resource-group $ResourceGroup `
  --name $KeyVaultName `
  --query id -o tsv

az role assignment create `
  --assignee-object-id $PrincipalId `
  --assignee-principal-type ServicePrincipal `
  --role "Key Vault Secrets User" `
  --scope $KeyVaultId
```

---

## 10. Configure Function App settings

After infra deploy, configure the app settings the Functions project needs at runtime.

### 10.1 Required DEV app settings

At minimum, plan for these values:

- `Entra__TenantId`
- `Entra__Authority`
- `Entra__ApiClientId`
- `Entra__MobileClientId`
- `Entra__ApiAudience` or equivalent
- `Entra__RequiredScope` or equivalent
- `Invite__BaseUrl`
- `KeyVaultName`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

Add any additional keys required by your Functions startup or middleware.

### 10.2 Set app settings with Azure CLI

```powershell
$ResourceGroup   = 'lusplit-rg-dev'
$FunctionAppName = 'lusplit-dev-fn'

$EntraTenantId   = '<DEV_CIAM_TENANT_ID>'
$EntraAuthority  = '<DEV_CIAM_AUTHORITY>'
$ApiClientId     = '<DEV_API_CLIENT_ID>'
$MobileClientId  = '<DEV_MOBILE_CLIENT_ID>'
$ApiAudience     = '<DEV_API_AUDIENCE>'
$RequiredScope   = '<DEV_REQUIRED_SCOPE>'
$InviteBaseUrl   = '<DEV_INVITE_BASE_URL>'

az functionapp config appsettings set `
  --resource-group $ResourceGroup `
  --name $FunctionAppName `
  --settings `
  Entra__TenantId=$EntraTenantId `
  Entra__Authority=$EntraAuthority `
  Entra__ApiClientId=$ApiClientId `
  Entra__MobileClientId=$MobileClientId `
  Entra__ApiAudience=$ApiAudience `
  Entra__RequiredScope=$RequiredScope `
  Invite__BaseUrl=$InviteBaseUrl
```

Use the exact setting names expected by your code. If your code uses another binding shape, match that shape.

---

## 11. Publish the Azure Functions app

Run from repo root:

```powershell
dotnet build .\LuSplit.slnx

dotnet publish .\src\LuSplit.Functions\LuSplit.Functions.csproj -c Release

func azure functionapp publish lusplit-dev-fn
```

If you publish through CI later, keep infra deploy and code publish as separate stages.

---

## 12. Local DEV configuration for the MAUI app

The app needs DEV values for authentication and the control plane base URL.

Inject at least:

- `Authority`
- `ExternalTenantId`
- `MobileClientId`
- `ApiClientId` or `ApiScope`
- `FunctionsBaseUrl`
- `InviteBaseUrl` or deep-link base

Put these in the mechanism already used by the app in DEV:

- local secrets file
- `LuSplit.App.secrets.props`
- platform-specific debug config
- environment-driven settings if already implemented

Do not hardcode DEV tenant IDs or URLs into committed source if you can avoid it.

---

## 13. GitHub Secrets to create

Create these secrets for the repo or environment used by DEV deployment.

### Azure deployment secrets

- `AZURE_SUBSCRIPTION_ID`
- `AZURE_TENANT_ID`
- `AZURE_CLIENT_ID`
- `AZURE_RESOURCE_GROUP_DEV`

If using OIDC federation, configure the GitHub → Azure trust and avoid client secrets.

### LuSplit DEV identity/config secrets

- `LUSPLIT_DEV_EXTERNAL_TENANT_ID`
- `LUSPLIT_DEV_ENTRA_AUTHORITY`
- `LUSPLIT_DEV_API_CLIENT_ID`
- `LUSPLIT_DEV_MOBILE_CLIENT_ID`
- `LUSPLIT_DEV_API_AUDIENCE`
- `LUSPLIT_DEV_REQUIRED_SCOPE`
- `LUSPLIT_DEV_INVITE_BASE_URL`
- `LUSPLIT_DEV_FUNCTIONS_BASE_URL`

### Optional publish-time or app config secrets

- `LUSPLIT_DEV_STORAGE_ACCOUNT_NAME`
- `LUSPLIT_DEV_KEY_VAULT_NAME`
- `LUSPLIT_DEV_FUNCTION_APP_NAME`

These last three can also be retrieved from Bicep outputs in the workflow instead of being stored as secrets.

---

## 14. Suggested GitHub Actions shape for DEV

Recommended DEV workflow stages:

1. Checkout
2. `dotnet build`
3. `dotnet test`
4. `az bicep build`
5. Azure login (OIDC recommended)
6. `az deployment group create`
7. Read outputs
8. `az functionapp config appsettings set`
9. Publish Functions
10. Optional smoke test

Keep these as distinct steps so infra and app deployment failures are easy to isolate.

---

## 15. Minimal DEV smoke test

After deploying DEV, validate this sequence:

### Smoke test A — auth and device bootstrap

1. Launch app on device A
2. Sign in
3. Verify device registration succeeds
4. Verify Functions endpoints requiring bearer auth are reachable

### Smoke test B — shared group creation

1. Create or convert a group to shared
2. Verify control plane group metadata exists
3. Verify remote storage container is created
4. Verify group is marked shared locally

### Smoke test C — invitation flow

1. Create invitation from owner device
2. Open link on device B
3. Sign in on device B
4. Accept invitation
5. Verify initial sync completes

### Smoke test D — sync

1. Add expense on device A
2. Trigger sync on device B
3. Verify expense appears and balances update

### Smoke test E — revocation

1. Revoke member or device
2. Trigger sync from revoked side
3. Verify authorization failure is explicit and expected
4. Verify remaining side continues to sync

---

## 16. Useful PowerShell helper commands

### Show deployed resources

```powershell
az resource list --resource-group lusplit-rg-dev -o table
```

### Show Function App URL

```powershell
az functionapp show `
  --resource-group lusplit-rg-dev `
  --name lusplit-dev-fn `
  --query defaultHostName -o tsv
```

### Show Function App settings

```powershell
az functionapp config appsettings list `
  --resource-group lusplit-rg-dev `
  --name lusplit-dev-fn -o table
```

### Show Key Vault name

```powershell
az keyvault list --resource-group lusplit-rg-dev -o table
```

### Show storage account name

```powershell
az storage account list --resource-group lusplit-rg-dev -o table
```

---

## 17. What should stay manual in Route 1

Keep these manual for now:

- Creating / managing the Entra External ID tenant
- Creating the mobile app registration
- Creating the API app registration
- Defining scopes / permissions / redirect URIs
- Copying the resulting IDs into DEV configuration

Keep these automated:

- Resource group infrastructure
- Function App and monitoring
- RBAC verification/fixup
- Function publish
- App settings application

---

## 18. Exit criteria for DEV readiness

DEV is ready when all of this is true:

- `az bicep build` passes
- `az deployment group create` succeeds
- Function App is running
- Function auth settings are present and correct
- MAUI app can sign in against DEV Entra External ID
- Device registration works
- Shared group creation works
- Invitation accept works
- Sync works across two devices
- Revocation blocks subsequent sync from the revoked side

---

## 19. Follow-up files recommended in `infra/`

Add these alongside this README:

- `infra/README.md` — this file
- `infra/SETTINGS_REFERENCE.md` — exact setting names consumed by Functions and MAUI app
- `infra/DEV_SMOKETEST.md` — step-by-step verification checklist
- `infra/PROD_DEPLOY.md` — later, same model with prod-specific hardening

---

## 20. Fast start checklist

If you want the shortest usable path:

1. Create DEV CIAM tenant / registrations manually
2. Patch `main.bicep` and `functions.bicep` to surface identity values
3. Fill `infra/parameters/dev.bicepparam`
4. Create `lusplit-dev` resource group
5. Deploy Bicep
6. Set Function App settings
7. Publish Functions
8. Configure MAUI DEV settings
9. Run the smoke test

That is the minimum route to a real DEV environment for this feature.
