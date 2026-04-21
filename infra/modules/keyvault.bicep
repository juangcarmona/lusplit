// Key Vault module — secrets for Functions app, RBAC for managed identity.
// Provides User Delegation SAS capability via Storage Blob Data Contributor role.

@description('Azure region for the deployment.')
param location string

@description('Short environment name (dev, prod).')
param environmentName string

@description('Base application name.')
param appName string

@description('Principal ID of the Functions app managed identity.')
param functionsPrincipalId string = ''

@description('Principal ID of the Storage account for User Delegation SAS.')
param storageAccountId string = ''

var keyVaultName = '${appName}-${environmentName}-kv'

// ── Key Vault ─────────────────────────────────────────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take(keyVaultName, 24)
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
    enableSoftDelete: true
  }
}

// ── RBAC: Functions managed identity → Key Vault Secrets User ────────────────

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource functionsKeyVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionsPrincipalId)) {
  name: guid(keyVault.id, functionsPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: functionsPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── RBAC: Functions managed identity → Storage Blob Data Contributor ─────────
// Required for User Delegation SAS generation.

var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource storageBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionsPrincipalId) && !empty(storageAccountId)) {
  name: guid(storageAccountId, functionsPrincipalId, storageBlobDataContributorRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionsPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri

