param location string

param appServicePlanName string

param webAppName string

param skuName string

param skuTier string

param skuCapacity int

param linuxFxVersion string

param applicationInsightsConnectionString string

param aspnetCoreEnvironment string

param tags object

var skuTierLower = toLower(skuTier)
var skuNameLower = toLower(skuName)
var skuIsFreeOrShared = skuTierLower == 'free' || skuTierLower == 'shared' || skuNameLower == 'f1' || skuNameLower == 'd1'

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    name: skuName
    tier: skuTier
    capacity: skuCapacity
  }
}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: '/health/ready'
      alwaysOn: skuIsFreeOrShared ? false : true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: aspnetCoreEnvironment
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'recommended'
        }
        {
          name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
          value: '2'
        }
      ]
    }
  }
}

output appServicePlanId string = appServicePlan.id

output appServicePlanName string = appServicePlan.name

output webAppId string = webApp.id

output webAppName string = webApp.name

output webAppDefaultHostname string = webApp.properties.defaultHostName

output webAppPrincipalId string = webApp.identity.principalId
