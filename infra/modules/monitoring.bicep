// Monitoring module — Application Insights workspace.
// Detailed implementation added in T112 (US7).

param location string
param environmentName string
param appName string

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-${environmentName}-logs'
  location: location
  properties: {
    retentionInDays: 30
    sku: { name: 'PerGB2018' }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-${environmentName}-ai'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

output appInsightsConnectionString string = appInsights.properties.ConnectionString

resource syncErrorAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${appName}-${environmentName}-sync-error-rate'
  location: location
  properties: {
    displayName: 'High Sync Error Rate'
    description: 'Alerts when sync error rate exceeds threshold over a 5-minute window'
    enabled: true
    scopes: [appInsights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'exceptions | where type == "SyncError" | summarize count() by bin(timestamp, 5m)'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 10
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    severity: 2
    autoMitigate: true
  }
}
