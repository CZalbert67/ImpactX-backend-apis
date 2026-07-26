using '../main.bicep'

param environmentName = 'prod'
param location = 'eastus2'
param resourceGroupName = 'rg-impactx-prod'
param namePrefix = 'impactx'
param appServicePlanSkuName = 'P1v3'
param appServicePlanSkuTier = 'PremiumV3'
param appServicePlanCapacity = 2
param linuxFxVersion = 'DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED'
param logAnalyticsRetentionInDays = 365
param tags = {
  environment: 'prod'
  managedBy: 'bicep'
  project: 'ImpactX'
}
