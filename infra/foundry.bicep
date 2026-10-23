@description('Azure region for the Foundry resource. Must have quota for the chosen models.')
param location string = resourceGroup().location

@description('Object ID of a user or managed identity that calls the models.')
param principalId string

@allowed(['User', 'ServicePrincipal'])
param principalType string = 'User'

param chatModel string = 'gpt-5-mini'
param chatModelVersion string = '2025-08-07'
@description('Thousands of tokens per minute.')
param chatCapacity int = 50

param embeddingModel string = 'text-embedding-3-small'
param embeddingModelVersion string = '1'
param embeddingCapacity int = 50

var suffix = uniqueString(resourceGroup().id)
var cognitiveServicesOpenAIUser = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource foundry 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: 'relay-foundry-${suffix}'
  location: location
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    customSubDomainName: 'relay-foundry-${suffix}'
    allowProjectManagement: true
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: foundry
  name: 'relay'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    displayName: 'Relay'
    description: 'AI agents for the Relay customer service hub'
  }
}

// Changes to one account must run one at a time, so these are chained.
resource chat 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundry
  name: chatModel
  dependsOn: [project]
  sku: { name: 'GlobalStandard', capacity: chatCapacity }
  properties: {
    model: { format: 'OpenAI', name: chatModel, version: chatModelVersion }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
  }
}

resource embedding 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: foundry
  name: embeddingModel
  dependsOn: [chat]
  sku: { name: 'GlobalStandard', capacity: embeddingCapacity }
  properties: {
    model: { format: 'OpenAI', name: embeddingModel, version: embeddingModelVersion }
  }
}

resource modelAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: foundry
  name: guid(foundry.id, principalId, cognitiveServicesOpenAIUser)
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUser)
  }
}

output endpoint string = 'https://${foundry.properties.customSubDomainName}.openai.azure.com/'
output projectEndpoint string = 'https://${foundry.properties.customSubDomainName}.services.ai.azure.com/api/projects/${project.name}'
output chatDeployment string = chat.name
output embeddingDeployment string = embedding.name
