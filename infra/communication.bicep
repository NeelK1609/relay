// Azure Communication Services for live chat and email notifications, with an Azure-managed sending domain.
// Apps authenticate with Entra ID (managed identity or az login); no access keys are used.

@description('Object ID of the identity that issues chat tokens and sends email.')
param principalId string

@allowed(['User', 'ServicePrincipal'])
param principalType string = 'ServicePrincipal'

@description('Where chat and email data is stored at rest.')
param dataLocation string = 'Canada'

param communicationName string = 'relay-acs-${uniqueString(resourceGroup().id)}'

// Aspire passes location to every module; Communication Services resources are global.
#disable-next-line no-unused-params
param location string = resourceGroup().location

var communicationAndEmailServiceOwner = '09976791-48a7-449e-bb21-39d1a415f350'

resource email 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: 'relay-email-${uniqueString(resourceGroup().id)}'
  location: 'global'
  properties: { dataLocation: dataLocation }
}

resource domain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: email
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource communication 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationName
  location: 'global'
  properties: {
    dataLocation: dataLocation
    linkedDomains: [domain.id]
  }
}

resource access 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: communication
  name: guid(communication.id, principalId, communicationAndEmailServiceOwner)
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', communicationAndEmailServiceOwner)
  }
}

output endpoint string = 'https://${communication.properties.hostName}/'
output senderAddress string = 'DoNotReply@${domain.properties.mailFromSenderDomain}'
