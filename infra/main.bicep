targetScope = 'subscription'

@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string

param location string

param resourceGroupName string

param namePrefix string

param appServicePlanSkuName string

param appServicePlanSkuTier string

param appServicePlanCapacity int

param linuxFxVersion string

param logAnalyticsRetentionInDays int

param tags object

var uniqueSuffix = uniqueString(subscription().id, resourceGroupName)

var appServicePlanName = 'asp-${namePrefix}-${environmentName}-${uniqueSuffix}'
var webAppName = 'app-${namePrefix}-${environmentName}-${uniqueSuffix}'
var logAnalyticsName = 'law-${namePrefix}-${environmentName}'
var applicationInsightsName = 'ai-${namePrefix}-${environmentName}'

var aspnetCoreEnvironment = environmentName == 'dev'
  ? 'Development'
  : environmentName == 'test'
    ? 'Test'
    : 'Production'

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module monitoring 'modules/monitoring.bicep' = {
  name: '${namePrefix}-${environmentName}-monitoring'
  scope: rg
  params: {
    location: location
    logAnalyticsName: logAnalyticsName
    applicationInsightsName: applicationInsightsName
    retentionInDays: logAnalyticsRetentionInDays
    tags: tags
  }
}

module appService 'modules/app-service.bicep' = {
  name: '${namePrefix}-${environmentName}-appservice'
  scope: rg
  params: {
    location: location
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    skuName: appServicePlanSkuName
    skuTier: appServicePlanSkuTier
    skuCapacity: appServicePlanCapacity
    linuxFxVersion: linuxFxVersion
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    aspnetCoreEnvironment: aspnetCoreEnvironment
    tags: tags
  }
}

output environmentName string = environmentName

output resourceGroupName string = rg.name

output appServicePlanName string = appServicePlanName

output webAppName string = webAppName

output webAppUrl string = 'https://${appService.outputs.webAppDefaultHostname}'

output webAppPrincipalId string = appService.outputs.webAppPrincipalId

output applicationInsightsName string = applicationInsightsName

output logAnalyticsWorkspaceName string = logAnalyticsName
