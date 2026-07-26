param location string

param logAnalyticsName string

param applicationInsightsName string

param retentionInDays int

param tags object

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name

output applicationInsightsId string = applicationInsights.id

output applicationInsightsName string = applicationInsights.name

output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
