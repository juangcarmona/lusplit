// Functions module — Azure Functions Consumption plan, app settings, managed identity.

param location string
param environmentName string
param appName string
param storageAccountName string
param keyVaultName string
param appInsightsConnectionString string

// Identity / auth settings injected from main.bicep
param externalTenantId string = ''
param apiClientId string = ''
param mobileClientId string = ''
param authority string = ''
param apiAudience string = ''
param requiredScope string = ''
param inviteBaseUrl string = ''

// RBAC role IDs
var storageAccountContributorRole = '17d1049b-9a84-46fb-8f53-869881c3d3ab' // Storage Account Contributor
var storageBlobDataOwnerRole = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b' // Storage Blob Data Owner
var storageQueueDataContributorRole = '974c5e8b-45b9-4653-ba55-5f855dd0fb88' // Storage Queue Data Contributor
var keyVaultSecretsUserRole = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User

resource hostingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${appName}-${environmentName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${appName}-${environmentName}-fn'
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        // Functions runtime
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'KeyVaultName', value: keyVaultName }

        // AzureWebJobsStorage with managed identity
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'AzureWebJobsStorage__blobServiceUri', value: 'https://${storageAccountName}.blob.core.windows.net' }
        { name: 'AzureWebJobsStorage__queueServiceUri', value: 'https://${storageAccountName}.queue.core.windows.net' }
        { name: 'AzureWebJobsStorage__tableServiceUri', value: 'https://${storageAccountName}.table.core.windows.net' }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }

        // Entra / auth wiring
        { name: 'Entra__TenantId', value: externalTenantId }
        { name: 'Entra__ApiClientId', value: apiClientId }
        { name: 'Entra__MobileClientId', value: mobileClientId }
        { name: 'Entra__Authority', value: authority }
        { name: 'Entra__ApiAudience', value: apiAudience }
        { name: 'Entra__RequiredScope', value: requiredScope }

        // App behavior
        { name: 'Invite__BaseUrl', value: inviteBaseUrl }
      ]
      netFrameworkVersion: 'v10.0'
    }
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Storage Account Contributor on the storage account
resource storageAccountContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, storageAccountContributorRole)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageAccountContributorRole)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Blob Data Owner on the storage account
resource storageBlobDataOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, storageBlobDataOwnerRole)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRole)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Queue Data Contributor on the storage account
resource storageQueueDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, storageQueueDataContributorRole)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRole)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User on the vault
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, keyVaultSecretsUserRole)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRole)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output functionAppName string = functionApp.name
output functionAppPrincipalId string = functionApp.identity.principalId
