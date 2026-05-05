targetScope = 'subscription'

@description('Environment name (dev, prod)')
param environment string = 'dev'

@description('Azure region')
param location string = 'eastus'

@description('ACR name')
param acrName string = 'tinyurlacr'

@description('AKS cluster name')
param aksClusterName string = 'tinyurl-aks'

var resourceGroupName = 'rg-tinyurl-${environment}'
var tags = {
  environment: environment
  project: 'TinyURL Gateway'
  managedBy: 'Bicep'
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    name: 'kv-tinyurl-${environment}'
    location: location
    tags: tags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  scope: rg
  params: {
    name: '${acrName}${environment}'
    location: location
    tags: tags
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  scope: rg
  params: {
    serverName: 'psql-tinyurl-${environment}'
    location: location
    tags: tags
    keyVaultName: keyVault.outputs.name
  }
}

module redis 'modules/redis.bicep' = {
  name: 'redis'
  scope: rg
  params: {
    name: 'redis-tinyurl-${environment}'
    location: location
    tags: tags
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  scope: rg
  params: {
    namespaceName: 'sb-tinyurl-${environment}'
    location: location
    tags: tags
  }
}

module aks 'modules/aks.bicep' = {
  name: 'aks'
  scope: rg
  params: {
    clusterName: '${aksClusterName}-${environment}'
    location: location
    tags: tags
    acrId: acr.outputs.id
    keyVaultName: keyVault.outputs.name
  }
}

module appInsights 'modules/appinsights.bicep' = {
  name: 'appinsights'
  scope: rg
  params: {
    name: 'ai-tinyurl-${environment}'
    location: location
    tags: tags
  }
}
