// Storage module — Azure Storage Account, blob containers, lifecycle policy.
// Detailed implementation added in T040 (US1).

param location string

@minLength(1)
param environmentName string

@minLength(1)
param appName string

var normalizedPrefix = toLower(replace('${appName}${environmentName}', '-', ''))

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take('${normalizedPrefix}storage', 24)
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

output storageAccountName string = storageAccount.name
