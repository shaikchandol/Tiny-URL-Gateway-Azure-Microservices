param namespaceName string
param location string
param tags object

resource sb 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
  properties: {
    zoneRedundant: true
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    disableLocalAuth: true
  }
}

resource urlEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: sb
  name: 'url-events'
  properties: {
    enablePartitioning: true
    defaultMessageTimeToLive: 'P7D'
  }
}

resource analyticsSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: urlEventsTopic
  name: 'analytics-subscription'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 5
  }
}

resource clickEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: sb
  name: 'click-events'
  properties: {
    enablePartitioning: true
    defaultMessageTimeToLive: 'P1D'
  }
}

resource clickAnalyticsSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: clickEventsTopic
  name: 'analytics-click-subscription'
  properties: {
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 10
  }
}

output namespaceFqdn string = '${sb.name}.servicebus.windows.net'
