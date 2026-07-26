using '../main.bicep'

param environmentName = 'test'
param location = 'eastus2'
param resourceGroupName = 'rg-impactx-test'
param namePrefix = 'impactx'
param appServicePlanSkuName = 'B1'
param appServicePlanSkuTier = 'Basic'
param appServicePlanCapacity = 1
param linuxFxVersion = 'DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED'
param logAnalyticsRetentionInDays = 30
param tags = {
  environment: 'test'
  managedBy: 'bicep'
  project: 'ImpactX'
}
