param name string
param location string
param tags object

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Premium' }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Disabled'
    zoneRedundancy: 'Enabled'
    policies: {
      trustPolicy: { type: 'Notary', status: 'enabled' }
      retentionPolicy: { days: 30, status: 'enabled' }
    }
  }
}

output id string = acr.id
output loginServer string = acr.properties.loginServer
