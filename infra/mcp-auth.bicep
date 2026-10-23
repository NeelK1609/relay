// Entra ID app registration that protects the Relay MCP server.
// Only identities granted the Crm.Tools app role (the API's managed identity) can call it.
extension 'br:mcr.microsoft.com/bicep/extensions/microsoftgraph/v1.0:1.0.0'

@description('Principal (object) ID of the managed identity allowed to call the MCP server.')
param callerPrincipalId string

// Aspire passes location to every module; Entra objects are global, so it's unused here.
#disable-next-line no-unused-params
param location string = resourceGroup().location

var uniqueName = 'relay-mcp-${uniqueString(resourceGroup().id)}'
var identifierUri = 'api://${tenant().tenantId}/${uniqueName}'
var crmToolsRoleId = guid(uniqueName, 'Crm.Tools')

resource app 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: uniqueName
  displayName: 'Relay MCP (${resourceGroup().name})'
  signInAudience: 'AzureADMyOrg'
  identifierUris: [identifierUri]
  api: {
    requestedAccessTokenVersion: 2
  }
  appRoles: [
    {
      id: crmToolsRoleId
      allowedMemberTypes: ['Application']
      displayName: 'Use Relay CRM tools'
      description: 'Call the Relay MCP server to read and update support cases.'
      value: 'Crm.Tools'
      isEnabled: true
    }
  ]
}

resource servicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: app.appId
}

resource callerGrant 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  appRoleId: crmToolsRoleId
  principalId: callerPrincipalId
  resourceId: servicePrincipal.id
}

output appId string = app.appId
output tenantId string = tenant().tenantId
output scope string = '${identifierUri}/.default'
