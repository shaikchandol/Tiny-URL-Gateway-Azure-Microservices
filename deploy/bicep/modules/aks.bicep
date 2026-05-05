param clusterName string
param location string
param tags object
param acrId string
param keyVaultName string

resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: clusterName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: clusterName
    agentPoolProfiles: [
      {
        name: 'system'
        count: 3
        vmSize: 'Standard_D4s_v3'
        mode: 'System'
        enableAutoScaling: true
        minCount: 3
        maxCount: 10
        osDiskSizeGB: 50
        osType: 'Linux'
        nodeTaints: []
      }
      {
        name: 'workload'
        count: 3
        vmSize: 'Standard_D8s_v3'
        mode: 'User'
        enableAutoScaling: true
        minCount: 3
        maxCount: 30
        osDiskSizeGB: 100
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      networkPolicy: 'azure'
      loadBalancerSku: 'standard'
    }
    addonProfiles: {
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: {
          enableSecretRotation: 'true'
          rotationPollInterval: '2m'
        }
      }
      omsAgent: {
        enabled: true
      }
      azurepolicy: {
        enabled: true
      }
      ingressApplicationGateway: {
        enabled: true
      }
    }
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
      defender: {
        securityMonitoring: {
          enabled: true
        }
      }
    }
    aadProfile: {
      managed: true
      enableAzureRBAC: true
    }
  }
}

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aks.id, acrId, 'acrpull')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: aks.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
  }
}

output clusterName string = aks.name
output kubeletIdentityObjectId string = aks.properties.identityProfile.kubeletidentity.objectId
