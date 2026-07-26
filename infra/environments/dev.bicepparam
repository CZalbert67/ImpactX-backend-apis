using '../main.bicep'

param environmentName = 'dev'
param location = 'eastus2'
param resourceGroupName = 'rg-impactx-dev'
param namePrefix = 'impactx'
param appServicePlanSkuName = 'F1'
param appServicePlanSkuTier = 'Free'
param appServicePlanCapacity = 1
param linuxFxVersion = 'DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED'
param logAnalyticsRetentionInDays = 30
param tags = {
  environment: 'dev'
  managedBy: 'bicep'
  project: 'ImpactX'
}
