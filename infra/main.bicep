// Bicep orchestrator for LuSplit shared-group infrastructure.
// Modules: storage, functions, keyvault, identity, monitoring.

targetScope = 'resourceGroup'

param location string = resourceGroup().location
param environmentName string
param appName string = 'lusplit'
param externalTenantId string = ''
param apiClientId string = ''
param mobileClientId string = ''
param authority string = ''
param apiAudience string = ''
param requiredScope string = ''
param inviteBaseUrl string = ''

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
  }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
  }
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    externalTenantId: externalTenantId
    apiClientId: apiClientId
    mobileClientId: mobileClientId
    authority: authority
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
  }
}

module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    storageAccountName: storage.outputs.storageAccountName
    keyVaultName: keyvault.outputs.keyVaultName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    externalTenantId: identity.outputs.externalTenantId
    apiClientId: identity.outputs.apiClientId
    mobileClientId: identity.outputs.mobileClientId
    authority: identity.outputs.authority
    apiAudience: apiAudience
    requiredScope: requiredScope
    inviteBaseUrl: inviteBaseUrl
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────

output storageAccountName string = storage.outputs.storageAccountName
output keyVaultName string = keyvault.outputs.keyVaultName
output functionAppName string = functions.outputs.functionAppName
output functionAppPrincipalId string = functions.outputs.functionAppPrincipalId
output externalTenantId string = identity.outputs.externalTenantId
output apiClientId string = identity.outputs.apiClientId
output mobileClientId string = identity.outputs.mobileClientId
output authority string = identity.outputs.authority
